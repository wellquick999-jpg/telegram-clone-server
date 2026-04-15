FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем всё (Shared внутри Server)
COPY TelegramClone.Server/ TelegramClone.Server/
COPY TelegramClone.Shared/ TelegramClone.Server/TelegramClone.Shared/

WORKDIR /src/TelegramClone.Server
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramClone.Server.dll"]