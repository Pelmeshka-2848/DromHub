# DromHub

## Database configuration

The application expects an Npgsql connection string named `ConnectionStrings:DromHub`. A placeholder value is provided in `appsettings.json`; replace it using one of the secret storage options below so credentials do not live in source control.

- **User secrets** (recommended for local development):

  ```bash
  dotnet user-secrets init --project DromHub/DromHub.csproj
  dotnet user-secrets set "ConnectionStrings:DromHub" "Host=localhost;Database=DromHubDB;Username=dromhub;Password=<your password>" --project DromHub/DromHub.csproj
  ```

- **Environment variable** (supports the default key and the existing `DROMHUB_` prefix):

  ```bash
  # Without a prefix (matches ASP.NET Core defaults)
  export ConnectionStrings__DromHub="Host=localhost;Database=DromHubDB;Username=dromhub;Password=<your password>"

  # Or with the optional DromHub-specific prefix
  export DROMHUB_ConnectionStrings__DromHub="Host=localhost;Database=DromHubDB;Username=dromhub;Password=<your password>"
  ```

If the application starts with the placeholder connection string still present, it will fail fast with an explanatory
error so that the database credentials never default to invalid values.

Ensure that deployment environments inject the same configuration value through their respective secret managers.
