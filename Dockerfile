# 1. Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the entire solution
COPY . .

# Restore dependencies for API project (this includes referenced projects)
RUN dotnet restore src/NursingCareBackend.Api/NursingCareBackend.Api.csproj

# Publish API project only
RUN dotnet publish src/NursingCareBackend.Api/NursingCareBackend.Api.csproj -c Release -o /app/out

# 2. Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

EXPOSE 5050
ENTRYPOINT ["dotnet", "NursingCareBackend.Api.dll"]
