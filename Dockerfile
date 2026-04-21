# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Leaf.sln ./
COPY App/Core/Leaf.Core/Leaf.Core.csproj App/Core/Leaf.Core/
COPY App/Features/Leaf.Features/Leaf.Features.csproj App/Features/Leaf.Features/
COPY App/Infrastructure/Leaf.Infrastructure/Leaf.Infrastructure.csproj App/Infrastructure/Leaf.Infrastructure/
COPY App/Runtime/Leaf.Runtime/Leaf.Runtime.csproj App/Runtime/Leaf.Runtime/
COPY App/UI/Leaf.Web/Leaf.Web.csproj App/UI/Leaf.Web/
COPY App/Tests/Leaf.Tests/Leaf.Tests.csproj App/Tests/Leaf.Tests/

RUN dotnet restore App/UI/Leaf.Web/Leaf.Web.csproj

COPY . .
RUN dotnet publish App/UI/Leaf.Web/Leaf.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV LeafDatabase__Path=/app/Runtime/leaf.db

RUN mkdir -p /app/Runtime

COPY --from=build /app/publish .

EXPOSE 8080
VOLUME ["/app/Runtime"]

ENTRYPOINT ["dotnet", "Leaf.Web.dll"]
