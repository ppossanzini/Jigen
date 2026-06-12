FROM mcr.microsoft.com/dotnet/aspnet:10.0 
WORKDIR /app
COPY publish .
EXPOSE 8000
EXPOSE 8001

RUN mkdir -p /data/onnx
RUN mkdir -p /data/jigendb

ENTRYPOINT ["dotnet", "Jigen.dll"]
