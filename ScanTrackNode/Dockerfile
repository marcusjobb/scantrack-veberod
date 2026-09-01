FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ScanTrackNode/ScanTrackNode.csproj ScanTrackNode/
RUN dotnet restore ScanTrackNode/ScanTrackNode.csproj

COPY ScanTrackNode/ ScanTrackNode/
COPY data/ data/
RUN dotnet publish ScanTrackNode/ScanTrackNode.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ScanTrackNode.dll"]
