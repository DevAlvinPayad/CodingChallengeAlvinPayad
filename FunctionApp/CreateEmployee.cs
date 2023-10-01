using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using FunctionApp.Extensions;
using FunctionApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
//using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;

namespace FunctionApp
{
    public class CreateEmployee
    {
        private readonly ILogger<CreateEmployee> _logger;

        public CreateEmployee(ILogger<CreateEmployee> log)
        {
            _logger = log;
        }

        [FunctionName("CreateEmployee")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "CreateEmployee" })]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Employee),  Description = "Employee details that will be saved in the cosmo db container")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Function CreateEmployee triggered successfully.")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
        [CosmosDB(
            databaseName: "CodingChallengeDB",
            containerName: "Employee",
            Connection = "DBConnectionString")]
        IAsyncCollector<dynamic> documentsOut)
        {
            _logger.LogInformation("HTTP trigger function processed a request.");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            _logger.LogInformation($"Request Payload: {requestBody}");

            var employeeJson = JsonConvert.DeserializeObject<Employee>(requestBody);

            _logger.LogInformation("HTTP trigger function processed a request.");
            try
            {
                if (string.IsNullOrEmpty(employeeJson.FirstName) || string.IsNullOrEmpty(employeeJson.LastName)
                       || string.IsNullOrEmpty(employeeJson.BirthdayInEpoch) || string.IsNullOrEmpty(employeeJson.Email))
                {
                    return new BadRequestObjectResult("Please fill in all the employee details.");
                }

                if (!Validator.IsValidEmail(employeeJson.Email))
                {
                    return new BadRequestObjectResult("Please enter a valid email.");
                }


                employeeJson.Id = Guid.NewGuid().ToString();
                await documentsOut.AddAsync(employeeJson);

                //save to event grid
                string topicEndpoint = Environment.GetEnvironmentVariable("EventGridEndpoint");
                string topicAccessKey = Environment.GetEnvironmentVariable("EventGridKey");
                
                //Creating client to publish events to eventgrid topic
                EventGridPublisherClient client = new EventGridPublisherClient(new Uri(topicEndpoint), new AzureKeyCredential(topicAccessKey));
               
                //Creating a sample event with Subject, Eventtype, dataVersion and data
                EventGridEvent egEvent = new EventGridEvent("Subject","Save Employee", "1.0", requestBody);
                // Send the event
                await client.SendEventAsync(egEvent);
                
            }
            catch(Exception ex)
            {
                string errMsg = "Function CreateEmployee encountered a problem in saving new Employee record";
                _logger.LogWarning(errMsg);
                return new BadRequestObjectResult(errMsg);
            }

            return new OkResult();
        }
    }
}

