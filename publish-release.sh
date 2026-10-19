
Version="${VARIABLE:-1.2.2}"

dotnet publish src/Server/Jigen/Jigen.csproj -o publish/server
dotnet publish src/Server/Jigen/Jigen-AllInOne.csproj -o publish/all-in-one-server
dotnet publish src/Embedding/Jigen.Embedding/Jigen.Embedding.csproj -o publish/embeddings
dotnet publish src/Embedding/Jigen.Embedding/Jigen.Embedding.csproj -p:JigenOnnxRuntimeFlavor=Gpu -o publish/embeddings-gpu
dotnet publish src/Embedding/Jigen.Embedding/Jigen.Embedding.csproj -p:JigenOnnxRuntimeFlavor=DirectML -o publish/embeddings-directml
dotnet publish src/Embedding/Jigen.Embedding/Jigen.Embedding.csproj -p:JigenOnnxRuntimeFlavor=OpenVino -o publish/embeddings-openvino
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

cd ../embeddings-gpu
podman build -f ../../src/Embedding/Jigen.Embedding/Dockerfile.gpu . -t ppossanzini/jigen-embeddings-gpu:latest -t ppossanzini/jigen-embeddings-gpu:$Version

cd ../embeddings-directml
podman build . -t ppossanzini/jigen-embeddings-directml:latest -t ppossanzini/jigen-embeddings-directml:$Version

cd ../embeddings-openvino
podman build . -t ppossanzini/jigen-embeddings-openvino:latest -t ppossanzini/jigen-embeddings-openvino:$Version

podman login docker.io
podman push ppossanzini/jigendb:latest
podman push ppossanzini/jigendb:$Version
podman push ppossanzini/jigendb-all-in-one:latest
podman push ppossanzini/jigendb-all-in-one:$Version
podman push ppossanzini/jigen-embeddings:latest
podman push ppossanzini/jigen-embeddings:$Version
podman push ppossanzini/jigen-embeddings-gpu:latest
podman push ppossanzini/jigen-embeddings-gpu:$Version
podman push ppossanzini/jigen-embeddings-directml:latest
podman push ppossanzini/jigen-embeddings-directml:$Version
podman push ppossanzini/jigen-embeddings-openvino:latest
podman push ppossanzini/jigen-embeddings-openvino:$Version
