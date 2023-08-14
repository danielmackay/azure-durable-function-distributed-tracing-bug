using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace ActivityBug
{
    public static class Function1
    {
        private const int NumActivities = 1000;

        [Function(nameof(Function1))]
        public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(Function1));
            logger.LogInformation("Starting Orchestration");

            await context.CallActivityAsync(nameof(GetCustomer));

            var customerNum = 0;
            var customerUpdated = false;

            do
            {
                customerUpdated = await context.CallActivityAsync<bool>(nameof(CreateBarcodes), customerNum);
                customerNum++;

            } while (customerUpdated);

            return "Finished";
        }

        [Function(nameof(CreateBarcodes))]
        public static async Task<bool> CreateBarcodes([ActivityTrigger] int customerNum, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CreateBarcodes));
            logger.LogInformation("Customer Number {CustomerNum}.", customerNum);
            await Task.Delay(10);

            if (customerNum <= 500)
                return true;

            return false;
        }

        [Function(nameof(GetCustomer))]
        public static async Task<string> GetCustomer([ActivityTrigger] FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(GetCustomer));
            logger.LogInformation("Activity Trigger");
            await Task.Delay(10);
            return "Bob Northwind";
        }

        [Function("Function1_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext,
            CancellationToken cancellationToken)
        {
            ILogger logger = executionContext.GetLogger("Function1_HttpStart");

            // Function input comes from the request content.
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(Function1), cancellationToken);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            // Returns an HTTP 202 response with an instance management payload.
            // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
