
Version="${VARIABLE:-1.1.2}"

dotnet publish src/Server/Jigen/Jigen.csproj -o publish/server
dotnet publish src/Server/Jigen/Jigen-AllInOne.csproj -o publish/all-in-one-server
dotnet publish src/Embedding/Jigen.TextEmbedding -o publish/embeddings
rm -rf publish/server/wwwroot/*
rm -rf publish/all-in-one-server/wwwroot/*

cd src/Management/Jigen.Insight 
npm run build 
cd ../../..
cp -r src/Management/Jigen.Insight/dist/* publish/server/wwwroot
cp -r src/Management/Jigen.Insight/dist/* publish/all-in-one-server/wwwroot

cd publish/server
podman build . -t ppossanzini/jigendb:latest -t ppossanzini/jigendb:$Version

cd ../../publish/all-in-one-server
podman build . -t ppossanzini/jigendb-all-in-one:latest -t ppossanzini/jigendb-all-in-one:$Version

cd ../../publish/embeddings
podman build . -t ppossanzini/jigen-embeddings:latest -t ppossanzini/jigen-embeddings:$Version

podman login docker.io
podman push ppossanzini/jigendb:latest
podman push ppossanzini/jigendb:$Version
podman push ppossanzini/jigendb-all-in-one:latest
podman push ppossanzini/jigendb-all-in-one:$Version
podman push ppossanzini/jigen-embeddings:latest
podman push ppossanzini/jigen-embeddings:$Version
