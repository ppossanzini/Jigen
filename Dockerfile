FROM mcr.microsoft.com/dotnet/aspnet:10.0 
WORKDIR /app
COPY publish .
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "Jigen.dll"]
