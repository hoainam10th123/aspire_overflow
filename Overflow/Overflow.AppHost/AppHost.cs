using Aspire.Hosting;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("production")
    .WithDashboard(dashboard => dashboard.WithHostPort(8080));

var keycloak = builder.AddKeycloak("keycloak", 6001)
    .WithDataVolume("keycloak-data")
    .WithEnvironment("KC_HTTP_ENABLED", "true")
    .WithEnvironment("KC_HOSTNAME_STRICT", "false")
    .WithEndpoint(port: 6001, targetPort: 8080, scheme: "http", name: "keycloak", isExternal: true);

var postgres = builder.AddPostgres("postgres", port: 5432)
    .WithDataVolume("postgres-data")
    .WithPgAdmin();

var questionDb = postgres.AddDatabase("questiondb");

var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithDataVolume("rabbitmq-data")
    .WithManagementPlugin(port: 15672);

var questionService = builder.AddProject<Projects.QuestionService>("question-svc")
    .WithReference(questionDb)
    .WithReference(keycloak)
    .WithReference(rabbitmq)
    .WaitFor(keycloak)
    .WaitFor(rabbitmq)
    .WaitFor(questionDb);// doi keycloak chay xong moi chay question service

//var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

var typesenseApiKey = builder.Configuration["TypesenseApiKey"];

var typesense = builder.AddContainer("typesense", "typesense/typesense", "29.0")
    .WithArgs("--data-dir", "/data", "--api-key", typesenseApiKey, "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typesenseContainer = typesense.GetEndpoint("typesense");

var searchService = builder.AddProject<Projects.SearchService>("search-svc")
    .WithEnvironment("typesense-api-key", typesenseApiKey)
    .WithReference(typesenseContainer)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq)
    .WaitFor(typesense);

var yarp = builder.AddYarp("gateway")
    .WithConfiguration(yarpBuilder =>
    {
        yarpBuilder.AddRoute("/questions/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/test/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/tags/{**catch-all}", questionService);
        yarpBuilder.AddRoute("/search/{**catch-all}", searchService);        
    })
    .WithEnvironment("ASPNETCORE_URLS", "http://*:8001")
    .WithEndpoint(port: 8001, targetPort: 8001, scheme: "http", name: "gateway", isExternal: true);
//.WithEnvironment("VIRTUAL_HOST", "overflow-api.trycatchlearn.com")
//.WithEnvironment("VIRTUAL_PORT", "8001")
//.WithEnvironment("LETSENCRYPT_HOST", "overflow-api.trycatchlearn.com")
//.WithEnvironment("LETSENCRYPT_EMAIL", "trycatchlearn@outlook.com");

if (!builder.Environment.IsDevelopment())
{
    builder.AddContainer("nginx-proxy", "nginxproxy/nginx-proxy", "1.8")
        .WithEndpoint(80, 80, "nginx", isExternal: true)
        .WithEndpoint(443, 443, "nginx-ssl", isExternal: true)
        .WithBindMount("/var/run/docker.sock", "/tmp/docker.sock", true)
        .WithVolume("certs", "/etc/nginx/certs", false)
        .WithVolume("html", "/usr/share/nginx/html", false)
        .WithVolume("vhost", "/etc/nginx/vhost.d")
        .WithContainerName("nginx-proxy");

    builder.AddContainer("nginx-proxy-acme", "nginxproxy/acme-companion", "2.2")
        .WithEnvironment("DEFAULT_EMAIL", "trycatchlearn@outlook.com")
        .WithEnvironment("NGINX_PROXY_CONTAINER", "nginx-proxy")
        .WithBindMount("/var/run/docker.sock", "/var/run/docker.sock", isReadOnly: true)
        .WithVolume("certs", "/etc/nginx/certs")
        .WithVolume("html", "/usr/share/nginx/html")
        .WithVolume("vhost", "/etc/nginx/vhost.d", false)
        .WithVolume("acme", "/etc/acme.sh");
}
//else
//{
//    keycloak.WithRealmImport("../infra/realms");
//}

builder.Build().Run();
