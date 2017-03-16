
#r "Newtonsoft.Json"
#r "Microsoft.WindowsAzure.Storage"

#load "recommendationWrapper.csx"
#load "Helpers.csx"

using Microsoft.WindowsAzure.Storage;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Net;
using System.Text;


public static async Task<HttpResponseMessage> Run(string iBlobRestaurants, string iBlobRatings, HttpRequestMessage req, TraceWriter log)
{
    string AccountKey = "COGNITIVE_SERVICES_KEY";
    string BaseUri = "COGNITIVE_SERVICE_URL";
    string modelName = "RecommenderModel";
    long buildId = 0;
   
    log.Info($"C# HTTP trigger function processed a request. RequestUri={req.RequestUri}");

    #region Parse query parameter
    string modelId = req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "modelId", true) == 0)
        .Value;

    long.TryParse( req.GetQueryNameValuePairs()
        .FirstOrDefault(q => string.Compare(q.Key, "buildId", true) == 0)
        .Value, out buildId);

    string userId = req.GetQueryNameValuePairs()
       .FirstOrDefault(q => string.Compare(q.Key, "userId", true) == 0)
       .Value;

    string itemId = req.GetQueryNameValuePairs()
       .FirstOrDefault(q => string.Compare(q.Key, "itemId", true) == 0)
       .Value;


    #endregion
       

    // inistaniate the recommender
    RecommendationsApiWrapper recommender = new RecommendationsApiWrapper(AccountKey, BaseUri);

    #region Create a model if not already provided.
    if (String.IsNullOrEmpty(modelId))
    {
        ModelInfo modelInfo = recommender.CreateModel(modelName);
        modelId = modelInfo.Id;
        log.Info("Model "+ modelInfo.Name + " created with ID: "+ modelId  );

    }
    #endregion

    #region If build is not provided, trigger a build with new data.
    if (buildId == 0)
    {
        // Upload Catalog and Usage data and then train the model (create a build)
        buildId = UploadDataAndTrainModel(recommender, modelId, iBlobRestaurants, iBlobRatings, "RestCatalog", "RestRatings" , log, BuildType.Recommendation);
    }
    #endregion

    List<dynamic> RecommendedItemIds = null;
    List<dynamic> RecommendedUserItems = null;

    // Get item-to-item recommendations and user-to-item recommendations one at a time
    if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(itemId))
    {
         RecommendedItemIds = GetRecommendationsSingleRequestForItem(recommender, itemId, modelId, buildId, log);
         RecommendedUserItems = GetRecommendationsSingleRequestForUser(recommender, userId, modelId, buildId, log);
    }
    else if (!string.IsNullOrEmpty(userId) && string.IsNullOrEmpty(itemId))
    { // User recommendation U2I
        RecommendedUserItems = GetRecommendationsSingleRequestForUser(recommender, userId, modelId, buildId, log);
    }
    else if (string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(itemId))
    { // item recommendation I2I
         RecommendedItemIds = GetRecommendationsSingleRequestForItem(recommender, itemId, modelId, buildId, log);
    }

    Dictionary<string, List<dynamic>> results = new Dictionary<string, List<dynamic>>();
    results.Add("Recommended Restaurants", RecommendedItemIds);
    results.Add("User Recommendation", RecommendedUserItems);

    return buildId == 0 || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId)
        ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a user id and item id on the query string or in the request body")
        : req.CreateResponse(HttpStatusCode.OK, results);
}

/// <summary>
/// Creates a model, upload catalog and usage files and trigger a build.
/// Returns the Build ID of the trained build.
/// </summary>
/// <param name="recommender">Wrapper that maintains API key</param>
/// <param name="buildType">The type of build. (Recommendation or FBT)</param>
/// <param name="modelId">The model Id</param>
public static long UploadDataAndTrainModel(RecommendationsApiWrapper recommender, string modelId,string catalogContentFile, string usageContentFile, string catalogName,string usageName, TraceWriter log, BuildType buildType = BuildType.Recommendation)
{
    long buildId = -1;
       

    // Import catalog data to the model.            
    log.Info("Start importing catalog file...");  
    recommender.UploadCatalog(modelId, catalogContentFile, catalogName);
    log.Info("End importing catalog file...");


    // Import usage data to the model.            

    log.Info("Start importing usage files...");    
    recommender.UploadUsage(modelId, usageContentFile, usageName);
    log.Info("End importing usage files...");

    #region training
    // Trigger a recommendation build.
    string operationLocationHeader;
    Console.WriteLine("Triggering build for model '{0}'. \nThis will take a few minutes...", modelId);
    if (buildType == BuildType.Recommendation)
    {
        buildId = recommender.CreateRecommendationsBuild(modelId, "Recommendation Build " + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                                                             enableModelInsights: false,
                                                             operationLocationHeader: out operationLocationHeader);
    }
    else
    {
        buildId = recommender.CreateFbtBuild(modelId, "Frequenty-Bought-Together Build " + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                                             enableModelInsights: false,
                                             operationLocationHeader: out operationLocationHeader);
    }

    // Monitor the build and wait for completion.
    log.Info("Monitoring build " + buildId.ToString());
    var buildInfo = recommender.WaitForOperationCompletion<BuildInfo>(RecommendationsApiWrapper.GetOperationId(operationLocationHeader));
    log.Info("Build "+ buildId.ToString() + " ended with status "+ buildInfo.Status + ".\n");

    if (String.Compare(buildInfo.Status, "Succeeded", StringComparison.OrdinalIgnoreCase) != 0)
    {
        log.Info("Build " + buildId + " did not end successfully, the sample app will stop here.");
        //log.Info("Press any key to end");
        //Console.ReadKey();
        return -1;
    }

    // Waiting  in order to propagate the model updates from the build...
    log.Info("Waiting for 40 sec for propagation of the built model...");
    Thread.Sleep(TimeSpan.FromSeconds(40));

    // The below api is more meaningful when you want to give a certain build id to be an active build.
    // Currently this app has a single build which is already active.
    log.Info("Setting build "+ buildId + " as active build." );
    recommender.SetActiveBuild(modelId, buildId);
    #endregion

    return buildId;
}


/// <summary>
/// Shows how to get item-to-item recommendations and user-to-item-recommendations
/// </summary>
/// <param name="recommender">Wrapper that maintains API key</param>
/// <param name="itemId">Item ID</param>
/// <param name="userId">User ID</param>
/// <param name="modelId">Model ID</param>
/// <param name="buildId">Build ID</param>
public static List<dynamic> GetRecommendationsSingleRequestForItem(RecommendationsApiWrapper recommender, string itemId, string modelId, long buildId, TraceWriter log)
{
    List<dynamic> ItemRecomList = new List<dynamic>();

    #region Get item to item recommendations. (I2I)
    log.Info("****************************************************************");
    log.Info("Displaying Item to Item "+ itemId);
    var itemSets = recommender.GetRecommendations(modelId, buildId, itemId, 3);
    if (itemSets.RecommendedItemSetInfo != null)
    {
        foreach (RecommendedItemSetInfo recoSet in itemSets.RecommendedItemSetInfo)
        {
            foreach (var item in recoSet.Items)
            {
                ItemRecomList.Add( new { ItemId = item.Id, ItemName = item.Name, Rating = recoSet.Rating });
                log.Info("Item id: "+ item.Id + " \n Item name: "+ item.Name + " \t (Rating  "+ recoSet.Rating + ")");
            }
        }
    }
    else
    {
        log.Info("No recommendations found.");
    }
    #endregion

    return ItemRecomList;
}



/// <summary>
/// Shows how to get item-to-item recommendations and user-to-item-recommendations
/// </summary>
/// <param name="recommender">Wrapper that maintains API key</param>
/// <param name="userId">User ID</param>
/// <param name="modelId">Model ID</param>
/// <param name="buildId">Build ID</param>
public static List<dynamic> GetRecommendationsSingleRequestForUser(RecommendationsApiWrapper recommender, string userId, string modelId, long buildId, TraceWriter log)
{
    List<dynamic> UserItemRecomList = new List<dynamic>();

    #region Now let's get a user recommendation (U2I)
    log.Info("****************************************************************");
    log.Info("Displaying User Recommendations for User: U1103");

    var itemSets = recommender.GetUserRecommendations(modelId, buildId, userId, 3);
    if (itemSets.RecommendedItemSetInfo != null)
    {
        foreach (RecommendedItemSetInfo recoSet in itemSets.RecommendedItemSetInfo)
        {
            foreach (var item in recoSet.Items)
            {
                UserItemRecomList.Add(new { ItemId = item.Id, ItemName = item.Name, Rating = recoSet.Rating });
                log.Info("Item id: " + item.Id + " \n Item name: " + item.Name + " \t (Rating  " + recoSet.Rating + ")");
            }
        }
    }
    else
    {
        log.Info("No recommendations found.");
    }
    #endregion

    return UserItemRecomList;
}
