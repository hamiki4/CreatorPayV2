FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore CreatorPay.slnx && dotnet publish src/CreatorPay.Worker/CreatorPay.Worker.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "CreatorPay.Worker.dll"]
