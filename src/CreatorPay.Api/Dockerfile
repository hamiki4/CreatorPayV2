FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY . .
RUN dotnet restore CreatorPay.slnx && dotnet publish src/CreatorPay.Api/CreatorPay.Api.csproj -c Release -o /app --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends gosu libgssapi-krb5-2 wget \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
COPY src/CreatorPay.Api/docker-entrypoint.sh /usr/local/bin/creatorpay-entrypoint
RUN chmod 0755 /usr/local/bin/creatorpay-entrypoint
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["/usr/local/bin/creatorpay-entrypoint"]
CMD ["dotnet", "CreatorPay.Api.dll"]
