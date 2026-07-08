FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY service/IaNewsPipeline.sln service/
COPY service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj service/IaNewsPipeline.Api/
COPY service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj service/IaNewsPipeline.Worker/

RUN dotnet restore service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj

COPY service/IaNewsPipeline.Api/ service/IaNewsPipeline.Api/
COPY service/IaNewsPipeline.Worker/ service/IaNewsPipeline.Worker/

RUN dotnet publish service/IaNewsPipeline.Worker/IaNewsPipeline.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IaNewsPipeline.Worker.dll"]
