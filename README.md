# Recommendations API Sample 

## Introduction
This function app is using content files; these files are stored in Azure blob storage to recommend items (Item to Item recommendation)
or recommend user's items (User to item recommendation).

This sample is a modified app from the sample recommendation console app that is stored [here](https://github.com/microsoft/Cognitive-Recommendations-Windows). 
You can find more details on the Recommendations API and other Cognitive Services at [www.microsoft.com/cognitive-services/](http://www.microsoft.com/cognitive-services/). 
 

The Recommendations API identifies consumption patterns from your transaction information in order to 
provide recommendations. These recommendations can help your customers more easily discover items that
they may be interested in.  By showing your customers products that they are more likely to be interested in,
you will, in turn, increase your sales.

## How to use this app:
1. Setup Recommendation API service in Azure portal.
2. Set Blob storage files in function.json file.
3. Set Access key in run.csx file for the provisioned cognitive service service in azure.
4. Set the base uri in run.csx file for the provisioned instance of cognitive service.
5. Set the Items catalog (Restaurant Catalog as i use it in this app) in the function.json file.
6. Set the User Transaction for items (Users' Restaurant Trnasactions) in the function.json file.
7. The function app connects to Azure blob storage to read required files to build a recommender model in 
Recommendation API in Cogntiive Services.
7. Run and debug the function app in VS locally by clicking on F5! Enjoy.

## Query String parameters
1. modelId (Optional): The model that the function will connect to, if not provided, the app will create a new model.
2. buildId (Optional): The build that the function will use, if not provided, the app will create a new model.
3. itemId (Required): The item id that the recommender model will use to provide item recommendation to a given item.
4. userId (Required): The user id that the recommender model will use to recommend items to a given user.

![Using postman to test the function app, here is the output](/Images/PostmanOutput.PNG)


## Description
The Function Application will:
1. Create a model container.
2. Add catalog and usage or transaction data needed to train the model.
3. Trigger a recommendation model build.
4. Monitor the training process, and notify you when the build has completed.
5. Use the newly created build to get recommendations.

## Source Code Files
- run.csx - The main file that included the function app code. 
- RecommendationsApiWrapper.cs - A wrapper that makes it easy to consume the recommendations API from C# 
- Helpers.cs - Helper file with classes to simplify serialization/deserialization of the RESTful API requests/responses. 


## More Information

[Azure Functions Developer Reference](https://docs.microsoft.com/en-us/azure/azure-functions/functions-reference)

[Working with Triggers and Bindings](https://docs.microsoft.com/en-us/azure/azure-functions/functions-triggers-bindings)

[Create your first Azure function in VS](https://blogs.msdn.microsoft.com/webdev/2016/12/01/visual-studio-tools-for-azure-functions/)

[Get started with Recommendation API](https://docs.microsoft.com/en-us/azure/cognitive-services/cognitive-services-recommendations-quick-start)

[Recommendation sample app on GitHub](https://github.com/microsoft/Cognitive-Recommendations-Windows)


Got questions? don't hesitate to contact me. You can follow me on Twitter: @MostafaElzoghbi

