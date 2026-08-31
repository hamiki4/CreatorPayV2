FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet restore CreatorPay.slnx && dotnet publish src/CreatorPay.Worker/CreatorPay.Worker.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --retries=4 CMD test -n "$(find /tmp/creatorpay-worker-ready -mmin -1 -print -quit)" || exit 1
ENTRYPOINT ["dotnet", "CreatorPay.Worker.dll"]
