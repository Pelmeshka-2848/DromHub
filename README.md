# DromHub


## Database configuration
The application expects an Npgsql connection string named `ConnectionStrings:DromHub`. A placeholder value is provided in `appsettings.json`; replace it using one of the secret storage options below so credentials do not live in source control.

- **User secrets** (recommended for local development):

  ```bash
  dotnet user-secrets init --project DromHub/DromHub.csproj
  dotnet user-secrets set "ConnectionStrings:DromHub" "Host=localhost;Database=DromHubDB;Username=dromhub;Password=<your password>" --project DromHub/DromHub.csproj
  ```

- **Environment variable** (respects the existing `DROMHUB_` prefix):

  ```bash
  export DROMHUB_ConnectionStrings__DromHub="Host=localhost;Database=DromHubDB;Username=dromhub;Password=<your password>"
  ```

Ensure that deployment environments inject the same configuration value through their respective secret managers.