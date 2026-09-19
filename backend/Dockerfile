FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY backend/FoodHelper.Api/FoodHelper.Api.csproj backend/FoodHelper.Api/
RUN dotnet restore backend/FoodHelper.Api/FoodHelper.Api.csproj

COPY backend/ backend/
RUN dotnet publish backend/FoodHelper.Api/FoodHelper.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

EXPOSE 5000

ENTRYPOINT ["dotnet", "FoodHelper.Api.dll"]
