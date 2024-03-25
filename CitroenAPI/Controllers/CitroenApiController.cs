﻿using Microsoft.AspNetCore.Mvc;
using ReptilApp.Api;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;
using CitroenAPI.Models;
using System.Text;
using System.Net.Http.Headers;
using System.Net;
using Newtonsoft.Json;
using System.Data.Common;
using static CitroenAPI.Models.Enums;



// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace CitroenAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CitroenApiController : ControllerBase
    {
        private readonly CitroenDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        string absolutePath;
        string absolutePathKEY;

        public CitroenApiController(CitroenDbContext context, IWebHostEnvironment hostingEnvironment)
        {
            _context = context;
            _hostingEnvironment = hostingEnvironment;
        }

        HttpListener.ExtendedProtectionSelector ExtendedProtectionSelector { get; set; }
        static X509Certificate2 clientCertificate;
        // GET: api/<ValuesController>
        [HttpGet]
        private async Task<string> Get()
        {
            try
            {
                string certificateFilePath = @".\Certificate\MZPDFMAP.cer";
                string certificatePassword = @".\Certificate\MZPDFMAP.pk"; // If the certificate is password-protected

                certificateFilePath = certificateFilePath.Replace(".\\", "");
                certificatePassword = certificatePassword.Replace(".\\", "");

                string currentDirectory = Environment.CurrentDirectory;

                absolutePath = System.IO.Path.Combine(_hostingEnvironment.ContentRootPath, certificateFilePath);
                absolutePathKEY = System.IO.Path.Combine(_hostingEnvironment.ContentRootPath, certificatePassword);

                clientCertificate = GetCert(absolutePath.ToString(), absolutePathKEY.ToString());

                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(clientCertificate);

                var loggingHandler = new HttpLoggingHandler(handler);

                var client = new HttpClient(loggingHandler);
                client.DefaultRequestHeaders.Add("User-Agent", "YourUserAgent");

                var requestUri = "https://api-secure.forms.awsmpsa.com/oauth/v2/token?client_id=5f7f179e7714a1005d204b43_2w88uv9394aok4g8gs0ccc4w4gwsskowck0gs0oo0sggw0kog0&client_secret=619ffmx8sn0g8ossso44wwok8scgoww00s8sogkw8w08cgc0wg&grant_type=password&username=ACMKPR&password=N9zTQ6v1";
                var response = await client.GetAsync(requestUri);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return ex.Message + " looking folder for: " + absolutePath;

            }
        }

        private X509Certificate2 GetCert(string certPath, string keyPath)
        {
            X509Certificate2 cert = new X509Certificate2(certPath);
            StreamReader reader = new StreamReader(keyPath);
            PemReader pemReader = new PemReader(reader);
            RsaPrivateCrtKeyParameters keyPair = (RsaPrivateCrtKeyParameters)pemReader.ReadObject();
            RSA rsa = DotNetUtilities.ToRSA(keyPair);
            cert = cert.CopyWithPrivateKey(rsa);
            return new X509Certificate2(cert.Export(X509ContentType.Pfx));
        }

        // POST api/<ValuesController>
        [HttpPost]
        public async Task<string> Post()
        {
            var handler = new HttpClientHandler();
            var resp = Get().Result;
            handler.ClientCertificates.Add(clientCertificate);
            using (var httpClient = new HttpClient(new HttpLoggingHandler(handler)))
            {
                try
                {
                    DateTime date = DateTime.Now;

                    DateTime sevenDays = date.AddDays(-7);

                    var dateRange = new
                    {
                        startDate = sevenDays.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                        endDate = date.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz")
                    };

                    string jsonDate = JsonConvert.SerializeObject(dateRange);

                    TokenAuth tokenObject = JsonConvert.DeserializeObject<TokenAuth>(resp);
                    var content = new StringContent(jsonDate, Encoding.UTF8, "application/json");
                    AuthenticationHeaderValue authHeader = new AuthenticationHeaderValue("Authorization", "Bearer " + tokenObject.access_token);

                    httpClient.DefaultRequestHeaders.Add("User-Agent", "YourUserAgent");
                    httpClient.DefaultRequestHeaders.Authorization = authHeader;

                    var response = await httpClient.PostAsync("https://api-secure.forms.awsmpsa.com/formsv3/api/leads", content);

                    string responseBody = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == HttpStatusCode.NotFound) 
                    {
                        return "No new leads";
                    }

                    RootObject responseData = JsonConvert.DeserializeObject<RootObject>(responseBody);

                    Logs logs = new Logs();

                    foreach (Message msg in responseData.message)
                    {
                        logs.GitId = msg.gitId;
                        logs.DispatchDate = msg.dispatchDate;
                        bool inserted =await AddLog(logs);
                        if(inserted)
                            await PostAsync(msg.leadData, msg.preferredContactMethod);

                        //Kodot za logika od baza ? dali veke postoi
                    }

                    return response.StatusCode.ToString();
                }
                catch (HttpRequestException e)
                {
                    return e.Message.ToString();
                }
            }

        }

        [HttpPost("AddLog")]
        public async Task<bool> AddLog(Logs logModel)
        {

            if (CheckLogs(logModel))
            {
                try
                {
                    _context.Logs.Add(logModel);

                    await _context.SaveChangesAsync();
                    return true;
                }
                catch (DbException ex)
                {
                    throw new Exception(ex.Message);
                    return false;
                }
              
            }
            else
            {
                return false;
            }

        }

        bool CheckLogs(Logs logsModel)
        {
            if (logsModel == null)
            {
                return false;
            }

            Logs res = _context.Logs.FirstOrDefault(model => model.GitId.Equals(logsModel.GitId));

            if (res == null)
            {
                return true;
            }
            else
                return false;

        }

        [HttpPost("SalesForce")]
        public async Task PostAsync(LeadData data, PreferredContactMethodEnum prefered)
        {
            string salutation = data.customer.civility==null ? "--None--": String.IsNullOrEmpty(Enums.GetEnumValue(data.customer.civility)) ? "--None-- " : Enums.GetEnumValue(data.customer.civility);
            string requestType = data.requestType==null? "--None--":String.IsNullOrEmpty(Enums.GetEnumValue(data.requestType)) ? "--None-- " : Enums.GetEnumValue(data.requestType);

            string url = "https://webto.salesforce.com/servlet/servlet.WebToLead?eencoding=UTF-8&orgId=00D7Q000004shjs" +
                "&salutation=" + salutation +
                "&first_name=" + data.customer.firstname +
                "&last_name=" + data.customer.lastname +
                "&email=" + data.customer.email +
                "&mobile=" + data.customer.personalMobilePhone +
                "&submit=submit&oid=00D7Q000004shjs&retURL=" +
                "&00N7Q00000KWlx2=" + requestType +
                "&lead_source=www.citroen.com.mk" +
                "&description=" + data.interestProduct.description +
                "&00N7Q00000KWlx7=" + prefered +
                "&00N7Q00000KWlxC=" + data.interestProduct.model +
                "&00N7Q00000KWlxH=TrebaInformacija";//Fali data;

                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, url);

            request.Headers.Add("Cookie", "BrowserId=asdasdasdasdasda");
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }
        
    }
}
