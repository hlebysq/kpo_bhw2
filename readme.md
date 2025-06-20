﻿# КР2. Синхронное межсервисное взаимодействие 
## Выполнил: Золотухин Глеб, БПИ236

### Суть работы серверной части веб-приложения

API должно принимать файлы от пользователей и проводить 
их анализ, включающий файл с общей информацией о содержимом
и строить облако слов. 

### Начало работы

Была использована следующая архитектура: 
сервис API gateway для routing'а запросов, 
File Storing Service для хранение и выдачу 
файлов и File Analysis Service для анализа 
файлов и выдачи этого анализа пользователю. 
Так как взаимодействие синхронное, нет смысла в 
брокере сообщений, поэтому было принято решение не 
использовать его.

### Разворачивание

Проект можно развернуть как локально, через мультизапуск, так и
через Docker - собран docker-compose файл со всеми параметрами.
При локальном запуске у всех сервисов будут localhost:xxxx адреса, 
при запуске через docker - осмысленные имена, это прописано в 
appsettings. 

### Поведение программы

#### API Gateway 

Перенаправляет пользователей на другие сервисы, 
возвращает им ответ от других сервисов. Реализовано 
с помощью технологии ocelot - в файле ocelot.json 
прописана конфигурация маршрутизации запросов. Весь обмен
информацией сервисов друг между другом и с пользователем
происходит через http запросы.

#### File Storing System

- Загрузка файла через /files/upload, возвращает 
пользователю id файла, [GET, POST] запрос. Если содержимое
файла уже есть в системе, пользователю выдается id уже 
существующего файла.
- Получение файла через files/{id}, [GET] запрос.
- Получение метаданных файла через /files/metadata/{id}, 
[GET] запрос.

В рамках данного сервиса также реализована интегрированная 
база данных. Для данного приложения была выбрана база данных 
SQLite, потому что она проста в развертывании и подходит для
небольших приложений, в случае масштабирования может подойти PostgreSQL.
Все файлы хранятся в FileStorage.

#### File Analysis System

- Анализ файла через /analysis/{fileId}, возвращает 
пользователю .txt файл с анализом исходного файла и создает облако слов.
Для создания облака сервис обращается к открытому 
API https://quickchart.io/documentation/word-cloud-api/
- Получение облака слов через /analysis/word-cloud/{fileId}, 
возвращает пользователю облако слов png картинкой по уже существующему файлу,
если оно было создано в рамках анализа.

Здесь тоже реализована интегрированная база данных, но с двумя таблицами: с
загруженными файлами и их путями и с результатами анализа, в каждом из которых
абсолютные пути на результат анализа и на облако. Все файлы хранятся в FileStorage.

### Сценарий работы

Пользователь загружает файл через files/upload, получает id файла.
Дальше он анализирует этот файл через analysis/{id} и скачивает его
облако слов через analysis/word-cloud/{id}. Если такой файл/анализ
уже существует в системе, пользователю возвращается id/существующий
анализ файла соответственно. Также можно скачать файл из системы по
его id.

### Обработка ошибок

Ошибки в сервисах обработаны на нескольких уровнях: 
внутри контроллеров всё обернуто в try catch'и, в 
Program'ах ошибки ловятся через Exception handler, 
в случае падения сервисы пытаются перезагрузиться 
пять раз (прописано в docker-compose файле). Также все действия
в контроллерах логирутся - при падении можно зайти в логи и посмотреть. 
Отдельно обработаны случаи, например, когда файл в базе данных не находится - 
пользователю возвращается ответ с кодом 404 not found.

### Unit-тесты

Для контроллеров сервисов FileStoringSystem и FileAnalysisSystem
добавлены Unit тесты, которые покрывают большую часть
функционала, включая как аварийные случаи (пустой файл/несуществующий
id), так и просто сценарии работы (загрузка тестового файла, 
получение для него анализа и облака слов и так далее). Тесты реализованы
с помощью фреймворка xUnit, для симуляции работы используют моки и 
тестовые данные. Тесты Запускаются они через команду 'dotnet test' в 
корне проекта. 

### Документация

Документация для каждого из сервисов реализована через
Swagger и доступна по адресу /swagger/index.html. Там можно
протестировать запросы напрямую к сервисам в режиме разработчика.