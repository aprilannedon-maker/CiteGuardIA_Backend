FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all files from repository
COPY . .

# Restore and publish from inside CiteGuardAPI
RUN dotnet restore "CiteGuardAPI/CiteGuardAPI.csproj"
RUN dotnet publish "CiteGuardAPI/CiteGuardAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CiteGuardAPI.dll"]