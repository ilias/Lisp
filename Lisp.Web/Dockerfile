FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Lisp.csproj ./
COPY Lisp.Web/Lisp.Web.csproj Lisp.Web/
RUN dotnet restore Lisp.Web/Lisp.Web.csproj -p:NuGetAudit=false

COPY . .
RUN dotnet publish Lisp.Web/Lisp.Web.csproj -c Release -o /app --no-restore -p:NuGetAudit=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# aspnet base image is Debian-based; already ships a non-root "app" user (uid 64198)
COPY --from=build /app .
USER app

ENTRYPOINT ["dotnet", "Lisp.Web.dll"]
