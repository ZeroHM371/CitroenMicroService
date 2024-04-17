﻿
using Azure;
using CitroenAPI.Logger;
using CitroenAPI.Models.DbContextModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Security;
using System.Diagnostics;
using System.Net.Mail;
using System.Net;

namespace CitroenAPI.Controllers
{
    public class SchadulerService : IHostedService, IDisposable
    {
        private readonly ILogger<SchadulerService> _logger;
        private Timer _timerForNext;
        private readonly CitroenDbContext _context;
        public IConfiguration _configuration { get; set; }
        private IServiceScopeFactory _scopeFactory;
        private Microsoft.AspNetCore.Hosting.IHostingEnvironment _environment;
        public SchadulerService(IServiceScopeFactory serviceScopeFactory,
            Microsoft.AspNetCore.Hosting.IHostingEnvironment environment, IConfiguration configuration, CitroenDbContext context,ILoggerFactory loggerFactory,ILogger<SchadulerService> logger)
        {
            loggerFactory.AddFile(Path.Combine(Directory.GetCurrentDirectory(), "logs"));
            _scopeFactory = serviceScopeFactory;
            _environment = environment;
            _configuration = configuration;
            _context = context;
            _logger = logger;
            _logger.LogInformation("--------------------------------------------------------------------------------");
            _logger.LogInformation("Constructor call in the service");
            _logger.LogInformation("--------------------------------------------------------------------------------");
        }
        public void Dispose()
        {
            _logger.LogInformation("Dispoce method was called inside constructor");
           // throw new NotImplementedException();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started the task async method");
            _timerForNext = new Timer(RunAgain, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));
            _logger.LogInformation("Executed the RunAgain method with result");
            return Task.CompletedTask;
        }

        private async void RunAgain(object? state)
        {
            _logger.LogInformation("==============================================================================================");
            _logger.LogInformation("Started RunAgain method");
            try
            {
                var client = new HttpClient();
                
                HttpRequestMessage request;
                _logger.LogInformation("--------------------------------------------------------------------------------");
                _logger.LogInformation("Created a httpClient and request");
                _logger.LogInformation("--------------------------------------------------------------------------------");
                if (!Debugger.IsAttached)
                {
                    request = new HttpRequestMessage(HttpMethod.Post, "https://cyberlink-001-site29.anytempurl.com/api/CitroenApi");
                    _logger.LogInformation("--------------------------------------------------------------------------------");
                    _logger.LogInformation("Debuger is not Attached " + request.ToString());
                    _logger.LogInformation("--------------------------------------------------------------------------------");
                }
                else
                {
                    request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:5216/api/CitroenApi");
                    _logger.LogInformation("--------------------------------------------------------------------------------");
                    _logger.LogInformation("Debugger is attached " + request.ToString());
                    _logger.LogInformation("--------------------------------------------------------------------------------");
                }
                request.Headers.Add("password", "b5267c1e130ec85238d12a4e5f2c85a1b185f7b7");
                _logger.LogInformation("Added Headers");
                var response = await client.SendAsync(request);
                _logger.LogInformation("Response result "+response.Content.ToString());
                _logger.LogInformation("--------------------------------------------------------------------------------");
                _logger.LogInformation("Response result is successfull check");
                _logger.LogInformation("--------------------------------------------------------------------------------");
                Console.WriteLine(await response.Content.ReadAsStringAsync());


            }
            catch (Exception ex)
            {
                //var smtpClient = new SmtpClient("smtp.gmail.com")
                //{
                //    Port = 587,
                //    Credentials = new NetworkCredential("npetrovski@ohanaone.mk", "NPohana1#*"),
                //    EnableSsl = true,
                //};

                //smtpClient.Send("npetrovski@ohanaone.mk", "npetrovski@ohanaone.mk", "CITROEN API", "ApiCall error" + ex.Message);

                _logger.LogError("1 ApiCall error " + ex.Message);

            }


        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Stop {cancellationToken} - Service was stopped");
            return Task.CompletedTask;
        }
    }
}
