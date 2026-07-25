using Autofac;
using Autofac.Extensions.DependencyInjection;
using RZPrime.Api.Utilities.Configurations;
using RZPrime.Api.Utilities.Middlewares;
using RZPrime.Services._Inventory._Hub;
using RZPrime.Services._User._Hub;
using RZPrime.Utilities.Configuration;
using Services._Price._RZPriceService;
using System.Text.Json.Serialization;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<IRZPriceService, RZPriceService>();

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

builder.WebHost.UseSentry(o =>
{
    o.Dsn = "https://75d965371f5b8296e9b50d0df7f55eb4@o4510492345368576.ingest.de.sentry.io/4510526128193616";
    //o.TracesSampleRate = 1.0;
    o.TracesSampleRate = 0;
    o.AttachStacktrace = true;
    o.SendDefaultPii = false;
    o.Debug = false;
    o.IncludeActivityData = false;
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

app.UseCustomRateLimiting();

app.UseAuthorization();

app.UseEndpoints();

app.MapHub<InventoryHub>("/hubs/inventory");
app.MapHub<PaidOrderHub>("/hubs/paidOrder");
app.MapHub<NonceNotifyHub>("/hubs/nonceNotify");

app.Run();