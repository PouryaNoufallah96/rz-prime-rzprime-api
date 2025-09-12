using Autofac;
using Autofac.Extensions.DependencyInjection;
using RZPrime.Api.Utilities.Configurations;
using RZPrime.Api.Utilities.Middlewares;
using RZPrime.Services._Inventory._Hub;
using RZPrime.Services._User._Hub;
using RZPrime.Utilities.Configuration;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);



builder.Services.AddCustomControllers();

builder.Services.AddCustomApiVersioning();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwagger();
builder.Services.AddHttpClient();

builder.Services.AddMemoryCache();

builder.Services.AddCodeAssistantSettings(builder.Configuration);
builder.Services.AddSettings(builder.Configuration);



builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(autofacConfigure =>
{
    autofacConfigure.AddServices();
    autofacConfigure.AddControllerServices();

});

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters
       .Add(new JsonStringEnumConverter());
});


var app = builder.Build();

app.UseHsts(app.Environment);

app.UseDeveloperExceptionPage(app.Environment);

app.UseSwaggerAndUI();

app.UseRequestLogger();

app.UseCustomExceptionHandler();

app.UseJWTBlackList();


app.UseProductionCors();

app.UseFirewall();

app.UseSignature();

app.UseJwt();

app.UseRouting();

//app.UseCustomRateLimiting();

app.UseAuthorization();

app.UseEndpoints();

app.MapHub<InventoryHub>("/hubs/inventory");
app.MapHub<PaidOrderHub>("/hubs/paidOrder");
app.MapHub<NonceNotifyHub>("/hubs/nonceNotify");

app.Run();