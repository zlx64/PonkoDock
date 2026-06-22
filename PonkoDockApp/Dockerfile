FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS base
USER root
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
ENV ASPNETCORE_HTTP_PORTS=8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PonkoDockApp/PonkoDockApp.csproj", "PonkoDockApp/"]
RUN dotnet restore "PonkoDockApp/PonkoDockApp.csproj"
COPY . .
WORKDIR "/src/PonkoDockApp"
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PonkoDockApp.dll"]
