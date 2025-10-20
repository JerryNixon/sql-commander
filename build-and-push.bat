@echo off
REM Build and push SQL Commander to Docker Hub

if "%1"=="" (
    echo Usage: build-and-push.bat ^<version^>
    echo Example: build-and-push.bat 1.0.0
    exit /b 1
)

echo Building Docker images...
docker build -f SqlCmdr.Web\Dockerfile -t jerrynixon/sql-commander:latest -t jerrynixon/sql-commander:%1 .

echo Pushing to Docker Hub...
docker push jerrynixon/sql-commander:latest
docker push jerrynixon/sql-commander:%1

echo Done!
