
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Collections.Generic;

namespace SubProvisioner
{
    public static class Function1
    {
        public static List<string> logs = new List<string>();
        public static string username = Environment.GetEnvironmentVariable("cspuser", EnvironmentVariableTarget.Process);
        public static string password = Environment.GetEnvironmentVariable("csppass", EnvironmentVariableTarget.Process);

        [FunctionName("Function1")]
        public static async Task<List<string>> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req, ILogger log)
        {
            logs.Clear();
            logs.Add("Received request");

            //Parse incoming request
            RequestBody body = new RequestBody();
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                body = JsonConvert.DeserializeObject<RequestBody>(requestBody);
                if (body.SubscriptionID!=null & body.TenantID!=null & body.UserEmail!=null)
                {
                    logs.Add("Request looks to be valid");
                }
                else
                {
                    logs.Add("Missing required fields in the request");
                    return logs;
                }
            }
            catch (Exception e)
            {
                logs.Add("Can not understand the request: " + e.Message);
                return logs;
            }


            //Call Graph
            GraphReply user = new GraphReply();
            using (var httpclient = new HttpClient())
            {
                try
                {
                    string token = Tools.GetAccessToken("https://graph.microsoft.com/", username, password, body.TenantID);
                    httpclient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                    logs.Add("Successfully requested token for MS Graph");
                }
                catch (Exception e)
                {
                    logs.Add("Failed to request token for MS Graph: " + e.Message);
                    return logs;
                }
                try
                {
                    string result = httpclient.GetStringAsync($"https://graph.microsoft.com/v1.0/users?$filter=startswith(userPrincipalName,'{body.UserEmail}')").Result;
                    user = JsonConvert.DeserializeObject<GraphReply>(result);
                    if (user.value[0].id!=null)
                    {
                        logs.Add($"Successfully found user with principalname: {user.value[0].userPrincipalName}, email: {user.value[0].mail} and id: {user.value[0].id}");
                    }
                    else
                    {
                        logs.Add($"Failed to find user with principalname: {body.UserEmail}");
                        return logs;
                    }

                }
                catch (Exception e)
                {
                    logs.Add("Failed to find user with specified email: " + e.Message);
                    return logs;
                }
            };

            //Format Azure request
            AzureRBAC permissions = new AzureRBAC()
            {
               properties = new RbacProperties
               {
                   //contributor - 	b24988ac-6180-42a0-ab88-20f7382dd24c
                   //owner - 8e3af657-a8ff-443c-a75c-2fe8c4bcb635
                   roleDefinitionId = $"/subscriptions/{body.SubscriptionID}/providers/Microsoft.Authorization/roleDefinitions/b24988ac-6180-42a0-ab88-20f7382dd24c",
                   principalId = user.value[0].id.ToString()
               }
            };

            //Call Azure
            using (var httpclient = new HttpClient())
            {
                try
                {
                    httpclient.DefaultRequestHeaders.Add("Authorization", "Bearer " + Tools.GetAccessToken("https://management.azure.com/", username, password, body.TenantID));
                    logs.Add("Successfully requested token for Azure Management");
                }
                catch (Exception e)
                {
                    logs.Add("Failed to request token for Azure Management: " + e.Message);
                    return logs;
                }
                try
                {
                    var jsonpayload = JsonConvert.SerializeObject(permissions);
                    StringContent content = new StringContent(jsonpayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage result = httpclient.PutAsync($"https://management.azure.com/subscriptions/{body.SubscriptionID}/providers/Microsoft.Authorization/roleAssignments/{Guid.NewGuid()}?api-version=2015-07-01", content).Result;
                    result.EnsureSuccessStatusCode();
                    logs.Add($"Successfully added permissions for user: {body.UserEmail} to subscription: {body.SubscriptionID} in tenant: {body.TenantID}");
                    //permissions = result.Content.ReadAsAsync<AzureRBAC>().Result;
                }
                catch (Exception e)
                {
                    logs.Add($"Failed to add permissions for user: {body.UserEmail} to subscription: {body.SubscriptionID} in tenant: {body.TenantID}");
                    return logs;
                }
            };
            return logs;
        }
    }
}
