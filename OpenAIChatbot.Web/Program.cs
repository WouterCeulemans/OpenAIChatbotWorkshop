using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Azure;
using OpenAIChatbot.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvcBuilder = builder.Services.AddControllersWithViews();
mvcBuilder.AddRazorRuntimeCompilation();

builder.Services.AddSignalR();
builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddClient<AzureOpenAIClient, AzureOpenAIClientOptions>((options) =>
    {
        var endpoint = builder.Configuration.GetValue<string>("AzureOpenAI:Endpoint")
            ?? throw new InvalidOperationException("Azure Open AI endpoint not provided");
        var key = builder.Configuration.GetValue<string>("AzureOpenAI:Key")
            ?? throw new InvalidOperationException("Azure Open AI key not provided");
        var endpointUrl = new Uri(endpoint);
        var credentials = new AzureKeyCredential(key);
        return new AzureOpenAIClient(endpointUrl, credentials, options);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

app.Run();
