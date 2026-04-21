# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Leaf.sln ./
COPY Leaf.Web/Leaf.Web.csproj Leaf.Web/
COPY Leaf.Tests/Leaf.Tests.csproj Leaf.Tests/

RUN dotnet restore Leaf.Web/Leaf.Web.csproj

COPY . .
RUN dotnet publish Leaf.Web/Leaf.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV LeafDatabase__Path=/app/Runtime/leaf.db

RUN mkdir -p /app/Runtime

COPY --from=build /app/publish .

EXPOSE 8080
VOLUME ["/app/Runtime"]

ENTRYPOINT ["dotnet", "Leaf.Web.dll"]
