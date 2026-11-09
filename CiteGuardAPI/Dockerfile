FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["CiteGuardAPI/CiteGuardAPI.csproj", "CiteGuardAPI/"]
RUN dotnet restore "CiteGuardAPI/CiteGuardAPI.csproj"

# Copy all source files and publish
COPY . .
WORKDIR "/src/CiteGuardAPI"
RUN dotnet publish "CiteGuardAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV PORT=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CiteGuardAPI.dll"]