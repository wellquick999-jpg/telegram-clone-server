FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TelegramClone.Shared/ TelegramClone.Shared/
COPY TelegramClone.Server/ TelegramClone.Server/
COPY TelegramClone.sln .

RUN dotnet restore TelegramClone.Server/TelegramClone.Server.csproj
RUN dotnet publish TelegramClone.Server/TelegramClone.Server.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramClone.Server.dll"]