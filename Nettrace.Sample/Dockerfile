#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Nettrace.Sample/Nettrace.Sample.csproj", "Nettrace.Sample/"]
COPY ["Nettrace/Nettrace.csproj", "Nettrace/"]
RUN dotnet restore "Nettrace.Sample/Nettrace.Sample.csproj"
COPY . .
WORKDIR "/src/Nettrace.Sample"
RUN dotnet build "Nettrace.Sample.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nettrace.Sample.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nettrace.Sample.dll"]