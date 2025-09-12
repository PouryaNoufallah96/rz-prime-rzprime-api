dotnet build RZPrime.Api/RZPrime.Api.csproj -c Release
dotnet publish RZPrime.Api/RZPrime.Api.csproj -c Release -o publish
docker login registry.gitlab.com
docker build -t registry.gitlab.com/rzprime/backend/rzprime.api .
docker push registry.gitlab.com/rzprime/backend/rzprime.api
pause