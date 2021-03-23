using System;
using Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ModelClasses;
using Azure.Data.Tables;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
//using Microsoft.Azure.WebJobs.Extensions.Storage;

namespace MortgageWebJob
{
    public class Functions
    {
        public Functions()
        {
        }

        public class MortgageInfoTableEntry : MortgageInfo, ITableEntity
        {
            public string PartitionKey { get; set; }
            public string RowKey { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; }
        }

        //NOTES FOR PROFESSOR
        //This is the code for table binding
        //Right now, I solved the error of the lectures by adding Microsot.Azure.WebJobs.Extensions.Storage package
        //But that generates another error in QueueTrigger that I couldn't solve.
        //[FunctionName("ProcessQueueMessageAsync")]
        //[return: Table("itmd519table")]
        public static void ProcessQueueMessageAsync([QueueTrigger("itmd519queue")] string message, ILogger logger)
        {

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var StorageAcctName = configuration.GetValue<string>("StorageAcctName");
            var TableName = configuration.GetValue<string>("TableName");
            var TableUrl = configuration.GetValue<string>("TableUrl");
            var TableKey = configuration.GetValue<string>("TableKey");

            var bytes = System.Text.Encoding.UTF8.GetBytes(message);
            var convertedString = System.Text.Encoding.Unicode.GetString(bytes);

            var mortgageInfo = JsonConvert.DeserializeObject<MortgageInfo>(convertedString);

            //Calculate the mortgage with MortgageHelper
            double mortgage = MortgageCalcHelper.ComputeMonthlyPayment(mortgageInfo.LoanAmount, mortgageInfo.Duration, mortgageInfo.Interest);
            string Message = $"The monthly payment is ${mortgage} for a loan of {mortgageInfo.LoanAmount} for {mortgageInfo.Duration} years and an interest rate of {mortgageInfo.Interest}";

            //write to table
            var tableClient = new TableClient(
                new Uri(TableUrl),
                TableName,
                new TableSharedKeyCredential(StorageAcctName, TableKey));


            //Define the entity
            MortgageInfoTableEntry mortgageInfoTableEntry = new MortgageInfoTableEntry()
            {
                PartitionKey = "Mortgage Information",
                RowKey = DateTime.Now.Ticks.ToString(),
                ETag = new ETag(),
                Timestamp = DateTime.Now,
                LoanAmount = mortgageInfo.LoanAmount,
                Interest = mortgageInfo.Interest,
                Duration = mortgageInfo.Duration,
                Mortgage = mortgage,
                Message = Message
            };

            tableClient.AddEntity(mortgageInfoTableEntry);

            logger.LogInformation($"MortgageInfo has a Loan Amount of {mortgageInfo.LoanAmount}, an Interest of {mortgageInfo.Interest}," +
                $" a Duration of {mortgageInfo.Duration}, a Mortgage amount of {mortgageInfo.Mortgage} and a Message: {mortgageInfo.Message}.");


            //return new MortgageInfoTableEntry { PartitionKey = "Mortgage Information", RowKey = DateTime.Now.Ticks.ToString(),
            //    LoanAmount = mortgageInfo.LoanAmount, Interest = mortgageInfo.Interest, Duration = mortgageInfo.Duration,
            //    Mortgage = mortgage, Message = Message
            //};
        } 
    }
}
