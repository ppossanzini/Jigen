FROM mcr.microsoft.com/dotnet/aspnet:10.0 
WORKDIR /app
COPY . .
EXPOSE 3223
EXPOSE 13223

RUN mkdir -p /data/onnx

ENTRYPOINT ["dotnet", "Jigen.TextEmbedding.dll"]
