using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Core;
using FunctionApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.WindowsAzure.Storage;
using Azure.Messaging.EventGrid;
using Azure;
using FunctionApp.Extensions;

namespace FunctionApp
{
    public class UpdateEmployee
    {
        private readonly ILogger<UpdateEmployee> _logger;

        public UpdateEmployee(ILogger<UpdateEmployee> log)
        {
            _logger = log;
        }

        [FunctionName("UpdateEmployee")]
        [OpenApiOperation(operationId: "Run", tags: new[] { "Update Employee" })]
        [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(Employee), Description = "Employee details that will be saved in the cosmo db container")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "Function CreateEmployee triggered successfully.")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "UpdateEmployee")] HttpRequest req,
        [CosmosDB(
            databaseName: "CodingChallengeDB",
            containerName: "Employee",
            Connection = "DBConnectionString")] CosmosClient client)
        {
            _logger.LogInformation($"C# HTTP trigger function processed a request. Function name: {nameof(UpdateEmployee)}");

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            _logger.LogInformation($"Request Payload: {requestBody}");

            var employeeJson = JsonConvert.DeserializeObject<Employee>(requestBody);

            if (string.IsNullOrWhiteSpace(employeeJson.Id))
            {
                return new NotFoundResult();
            }

            _logger.LogInformation("HTTP trigger function processed a request.");
            try
            {
                Container container = client.GetDatabase("CodingChallengeDB").GetContainer("Employee");

                _logger.LogInformation($"Searching for: {employeeJson.Id}");

                Microsoft.Azure.Cosmos.PartitionKey partitionKey = new(employeeJson.Id);
                Employee employee = await container.ReadItemAsync<Employee>(employeeJson.Id, partitionKey);
                if (employee != null)
                {
                    if(string.IsNullOrEmpty(employeeJson.FirstName) || string.IsNullOrEmpty(employeeJson.LastName)
                        || string.IsNullOrEmpty(employeeJson.BirthdayInEpoch) || string.IsNullOrEmpty(employeeJson.Email))
                    {
                        return new BadRequestObjectResult("Please fill in all the employee details.");
                    }

                    if (!Validator.IsValidEmail(employeeJson.Email))
                    {
                        return new BadRequestObjectResult("Please enter a valid email.");
                    }

                    employee.FirstName= employeeJson.FirstName;
                    employee.LastName= employeeJson.LastName;
                    employee.BirthdayInEpoch= employeeJson.BirthdayInEpoch;
                    employee.Email = employeeJson.Email;
                    await container.UpsertItemAsync(employee);
                    _logger.LogInformation("Employee: " + employeeJson.Id + " has been updated.");


                    //save to event grid
                    string topicEndpoint = Environment.GetEnvironmentVariable("EventGridEndpoint");
                    string topicAccessKey = Environment.GetEnvironmentVariable("EventGridKey");

                    //Creating client to publish events to eventgrid topic
                    EventGridPublisherClient eventClient = new EventGridPublisherClient(new Uri(topicEndpoint), new AzureKeyCredential(topicAccessKey));

                    //Creating a sample event with Subject, Eventtype, dataVersion and data
                    EventGridEvent egEvent = new EventGridEvent("Subject", "Save Employee", "1.0", requestBody);
                    // Send the event
                    await eventClient.SendEventAsync(egEvent);
                }
                else
                {
                    return new NotFoundResult();
                }
            }
            catch (Exception ex)
            {
                string errMsg = "Function UpdateEmployee encountered a problem in updating Employee record";
                _logger.LogWarning(errMsg);
                return new BadRequestObjectResult(errMsg);
            }


            return new OkResult();
        }
    }
}

