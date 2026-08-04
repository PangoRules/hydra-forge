FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY HydraForge.slnx ./
COPY src ./src
COPY tests ./tests
RUN dotnet restore HydraForge.slnx
RUN dotnet publish src/HydraForge.Server/HydraForge.Server.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
# Built into the aspnet base image — not created here, just switched to.
USER app
ENTRYPOINT ["dotnet", "HydraForge.Server.dll"]