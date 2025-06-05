FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["FileStoringService/FileStoringService.csproj", "FileStoringService/"]
RUN dotnet restore "FileStoringService/FileStoringService.csproj"

COPY . .
WORKDIR "/src/FileStoringService"
RUN dotnet build "FileStoringService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FileStoringService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Создаем директорию для файлов
RUN mkdir -p /app/uploads
VOLUME /app/uploads

ENTRYPOINT ["dotnet", "FileStoringService.dll"]