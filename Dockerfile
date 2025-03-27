# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

# Устанавливаем рабочую директорию внутри контейнера
WORKDIR /src

# Копируем все проекты из локальной машины в контейнер
COPY ["Monitoring.Api/Monitoring.Api.csproj", "Monitoring.Api/"]
COPY ["Monitoring.Application/Monitoring.Application.csproj", "Monitoring.Application/"]
COPY ["Monitoring.Domain/Monitoring.Domain.csproj", "Monitoring.Domain/"]
COPY ["Monitoring.Infrastructure/Monitoring.Infrastructure.csproj", "Monitoring.Infrastructure/"]

# Восстанавливаем зависимости
RUN dotnet restore "Monitoring.Api/Monitoring.Api.csproj"

# Копируем оставшиеся файлы
COPY . .

# Строим проект
WORKDIR "/src/Monitoring.Api"
RUN dotnet publish "Monitoring.Api.csproj" -c Release -o /app/publish

# Строим образ для запуска
FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

# Копируем собранный проект из предыдущего шага
COPY --from=build /app/publish .

# Указываем команду для запуска приложения
ENTRYPOINT ["dotnet", "Monitoring.Api.dll"]