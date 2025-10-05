#!/bin/bash

PROJECT_DIR="$(pwd)"


IMAGE_NAME="rzprime.api"
CONTAINER_NAME="api.rzprime.com"


echo "Building and publishing the project..."
dotnet build $PROJECT_DIR/RZPrime.Api/RZPrime.Api.csproj -c Release
dotnet publish $PROJECT_DIR/RZPrime.Api/RZPrime.Api.csproj -c Release -o $PROJECT_DIR/publish

echo " Building Docker image..."
docker build -t $IMAGE_NAME $PROJECT_DIR

echo "Stopping old container if exists..."
docker-compose down


echo "Starting container..."
docker-compose up -d


echo "Container status:"
docker-compose ps

echo "List of all containers:"
docker ps -a

echo "Showing logs (press Ctrl+C to exit)..."
docker-compose logs -f
