
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
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace SubProvisioner
{
    public static class Function2
    {
        public static List<string> logs = new List<string>();
        public static string username = Environment.GetEnvironmentVariable("cspuser", EnvironmentVariableTarget.Process);
        public static string password = Environment.GetEnvironmentVariable("csppass", EnvironmentVariableTarget.Process);

        [FunctionName("Function2")]
        public static async Task<List<string>> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)]HttpRequest req)
        {
            logs.Clear();
            logs.Add("Received request");

            //Parse incoming request
            DeployRequest body = new DeployRequest();
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                body = JsonConvert.DeserializeObject<DeployRequest>(requestBody);
                if (body.SubscriptionID != null & body.TenantID != null & body.TemplateUri != null & body.ResourceGroup != null & body.ResourceGroupLocation != null)
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

            //Format Azure request
            AzureDeployment deploy = new AzureDeployment()
            {
                properties = new DeploymentProperties
                {
                    templateLink = new TemplateLink { uri = body.TemplateUri, contentVersion = "1.0.0.0" },
                    parametersLink = new ParametersLink { uri = body.ParametersUri, contentVersion = "1.0.0.0" },
                    mode = "Incremental"
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
                    HttpResponseMessage result = httpclient.GetAsync($"https://management.azure.com/subscriptions/{body.SubscriptionID}/resourcegroups/{body.ResourceGroup}?api-version=2018-02-01").Result;
                    if (result.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        logs.Add("Looks like resource group doesn't exist, creating it...");
                        StringContent rg = new StringContent(JsonConvert.SerializeObject(new { location = body.ResourceGroupLocation }), Encoding.UTF8, "application/json");
                        HttpResponseMessage createrg = httpclient.PutAsync($"https://management.azure.com/subscriptions/{body.SubscriptionID}/resourcegroups/{body.ResourceGroup}?api-version=2018-02-01", rg).Result;
                        createrg.EnsureSuccessStatusCode();
                        logs.Add($"Successfully created resource group '{body.ResourceGroup}'");
                    }
                }
                catch (Exception)
                {
                    logs.Add($"Failed to create resource group '{body.ResourceGroup}'");
                    return logs;
                }
                try
                {
                    var jsonpayload = JsonConvert.SerializeObject(deploy);
                    StringContent content = new StringContent(jsonpayload, Encoding.UTF8, "application/json");
                    HttpResponseMessage result = httpclient.PutAsync($"https://management.azure.com/subscriptions/{body.SubscriptionID}/resourcegroups/{body.ResourceGroup}/providers/Microsoft.Resources/deployments/cspadminagent?api-version=2015-01-01", content).Result;
                    result.EnsureSuccessStatusCode();
                    logs.Add($"Successfully deployed template: {body.TemplateUri} to resourcegroup: {body.ResourceGroup} in subscription: {body.SubscriptionID} for tenant: {body.TenantID}");
                }
                catch (Exception e)
                {
                    logs.Add($"Failed to deploy template: {body.TemplateUri} to subscription: {body.SubscriptionID} in tenant: {body.TenantID}");
                    return logs;
                }
            };
            return logs;
        }
    }
}
