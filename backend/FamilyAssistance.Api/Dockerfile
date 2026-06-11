FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FamilyAssistance.Api/FamilyAssistance.Api.csproj FamilyAssistance.Api/
RUN dotnet restore FamilyAssistance.Api/FamilyAssistance.Api.csproj
COPY FamilyAssistance.Api/ FamilyAssistance.Api/
RUN dotnet publish FamilyAssistance.Api/FamilyAssistance.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FamilyAssistance.Api.dll"]
