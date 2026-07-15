# Google.Cloud.Spanner.Data

Official **ADO.NET data provider** for [Google Cloud Spanner](https://cloud.google.com/spanner).

📚 **[Full Documentation & API Reference](https://cloud.google.com/dotnet/docs/reference/Google.Cloud.Spanner.Data/latest)**

## Installation

```bash
dotnet add package Google.Cloud.Spanner.Data
```

## Getting Started

### 1. Authentication
By default, the library uses [Application Default Credentials (ADC)](https://cloud.google.com/docs/authentication#adc). Locally, authenticate with the Google Cloud CLI:

```bash
gcloud auth application-default login
```

### 2. Dependency Injection (.NET / ASP.NET Core)
Register `SpannerConnection` as a transient dependency directly from your `appsettings.json` `ConnectionStrings` section:

```json
{
  "ConnectionStrings": {
    "SpannerDb": "Data Source=projects/my-project/instances/my-instance/databases/my-database"
  }
}
```

```csharp
builder.Services.AddSpannerConnection(builder.Configuration);
```

### 3. Querying Cloud Spanner

```csharp
using Google.Cloud.Spanner.Data;

// Or inject SpannerConnection directly into your service/controller
using var connection = new SpannerConnection(connectionString);

var cmd = connection.CreateSelectCommand("SELECT SingerId, FirstName, LastName FROM Singers");
using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"{reader["SingerId"]}: {reader["FirstName"]} {reader["LastName"]}");
}
```

## Related Packages

- **[Google.Cloud.EntityFrameworkCore.Spanner](https://www.nuget.org/packages/Google.Cloud.EntityFrameworkCore.Spanner)**: Entity Framework Core provider for Cloud Spanner.
- **[Google.Cloud.Spanner.Admin.Instance.V1](https://www.nuget.org/packages/Google.Cloud.Spanner.Admin.Instance.V1)**: Instance administration (create/manage instances).
- **[Google.Cloud.Spanner.Admin.Database.V1](https://www.nuget.org/packages/Google.Cloud.Spanner.Admin.Database.V1)**: Database administration and schema management.
