using Scalar.AspNetCore;
using USAePayPortal;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration);

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors(ServiceComponentsDI.FrontendCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
