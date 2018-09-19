using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace SubProvisioner
{
    public class Tools
    {
        public static string GetAccessToken(string resource, string username, string password, Guid TenantID)
        {
            const string grantType = "password";
            const string client_id = "1950a258-227b-4e31-a9cf-717495945fc2";
            string getTokenUrl = $"https://login.microsoftonline.com/{TenantID.ToString()}/oauth2/token";
            string postBody = $"resource={resource}&grant_type={grantType}&username={username}&password={password}&client_id={client_id}";

            //Get access token
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, getTokenUrl);
            httpRequestMessage.Content = new StringContent(postBody, Encoding.UTF8, "application/x-www-form-urlencoded");
            HttpClient client = new HttpClient();
            HttpResponseMessage httpResponseMessage = client.SendAsync(httpRequestMessage).Result;
            string responseBody = httpResponseMessage.Content.ReadAsStringAsync().Result;
            return JObject.Parse(responseBody).GetValue("access_token").ToString();
        }
    }

    public class ProvisionRequest
    {
        public Guid TenantID { get; set; }
        public Guid SubscriptionID { get; set; }
        public string UserEmail { get; set; }

    }

    public class DeployRequest
    {
        public Guid TenantID { get; set; }
        public Guid SubscriptionID { get; set; }
        public string TemplateUri { get; set; }
        public string ParametersUri { get; set; }
        public string ResourceGroup { get; set; }
        public string ResourceGroupLocation { get; set; }
    }

    public class GraphUser
    {
        public Guid id { get; set; }
        public string mail { get; set; }
        public string userPrincipalName { get; set; }
    }

    public class GraphReply
    {
        public List<GraphUser> value { get; set; }
    }

    public class AzureRBAC
    {
        public string id { get; set; }
        public string type { get; set; }
        public string name { get; set; }
        public RbacProperties properties { get; set; }
    }

    public class RbacProperties
    {
        public string roleDefinitionId { get; set; }
        public string principalId { get; set; }
        public string scope { get; set; }
        public string createdOn { get; set; }
        public string updatedOn { get; set; }
        public string createdBy { get; set; }
        public string updatedBy { get; set; }
    }
    public class TemplateLink
    {
        public string uri { get; set; }
        public string contentVersion { get; set; }
    }

    public class ParametersLink
    {
        public string uri { get; set; }
        public string contentVersion { get; set; }
    }

    public class DeploymentProperties
    {
        public TemplateLink templateLink { get; set; }
        public string mode { get; set; }
        public ParametersLink parametersLink { get; set; }
    }

    public class AzureDeployment
    {
        public DeploymentProperties properties { get; set; }
    }
}
