using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FileUpload.Models;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Storage.Blob;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage;
using Azure.Storage.Sas;
using UserDelegationKey = Azure.Storage.Blobs.Models.UserDelegationKey;
using SendGrid;
using SendGrid.Helpers.Mail;
using Newtonsoft.Json.Linq;

namespace FileUpload.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private IConfiguration config;
        private string accessKey="";
        public HomeController(ILogger<HomeController> logger,IConfiguration configuration)
        {
            config = configuration;
            _logger = logger;
            accessKey = config.GetSection("BLOB_KEY").Value;
                
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Upload(string email,IFormFile file)
        {
            try
            {
                //connect to azure
                CloudStorageAccount account = CloudStorageAccount.Parse(accessKey);

                CloudBlobClient client = account.CreateCloudBlobClient();

                //get blob container (on azure)
                CloudBlobContainer container = client.GetContainerReference("myblobs");
                //get refrence to the blob set with filename 
                CloudBlockBlob blob = container.GetBlockBlobReference(file.FileName);

                using (var fileStream = file.OpenReadStream())
                {
                    await blob.UploadFromStreamAsync(fileStream);
                }
                //var udk = client.GetUserDelegationKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7)).Value;
               string url= SendSAS(blob);
               await CreateEmailAsync(email, url);
               await TellMe(); 
               return View("Done");

            }
            catch
            {
               
                return View("Error");
            }
        }

        private async Task TellMe()
        {
            string apiKey = config.GetSection("SENDGRID_API_KEY").Value;
            SendGridClient client = new SendGridClient(apiKey);
            string msg = ("Someone uploaded something to your blob");
            SendGridMessage mail = new SendGridMessage
            {
                From = new EmailAddress("willschultz2@gmail.com"),
                Subject = "Blob Alert",
                HtmlContent = msg
            };
            mail.AddTo("willschultz2@gmail.com");
            await client.SendEmailAsync(mail);
        }

        private async Task CreateEmailAsync(string email, string url)
        {

                 email = email.Trim();
            string apiKey = config.GetSection("SENDGRID_API_KEY").Value;
            SendGridClient client = new SendGridClient(apiKey);
            string msg = ("<p>Your link is ready:" + url + "</p>" +
                "<p>It will expire 7 days from now</p>");
            SendGridMessage mail = new SendGridMessage
            {
                From = new EmailAddress("willschultz2@gmail.com"),
                Subject = "Your Uploaded File",
                HtmlContent = msg
            };
            mail.SetOpenTracking(true);
            mail.AddTo(email);
            await client.SendEmailAsync(mail);
     
        }

        private string SendSAS( CloudBlockBlob blob)
        {
           
            SharedAccessBlobPolicy p = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddDays(7),
                Permissions = SharedAccessBlobPermissions.Read
            };

            string token = blob.Uri+ blob.GetSharedAccessSignature(p);
            //string token = builder.ToSasQueryParameters(udk,blob);
            return token;
        }

       
    }
}
