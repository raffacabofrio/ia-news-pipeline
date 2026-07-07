FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY service/IaNewsPipeline.sln service/
COPY service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj service/IaNewsPipeline.Api/

RUN dotnet restore service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj

COPY service/IaNewsPipeline.Api/ service/IaNewsPipeline.Api/

RUN dotnet publish service/IaNewsPipeline.Api/IaNewsPipeline.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "IaNewsPipeline.Api.dll"]
