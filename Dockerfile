FROM mcr.microsoft.com/dotnet/aspnet:10.0 
WORKDIR /app
COPY publish .
EXPOSE 3223
EXPOSE 13223

RUN mkdir -p /data/onnx
RUN mkdir -p /data/jigendb

ENTRYPOINT ["dotnet", "Jigen.dll"]
