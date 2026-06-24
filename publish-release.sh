
Version="${VARIABLE:-1.0.7}"

dotnet publish src/Server/Jigen -o publish 

rm -rf publish/wwwroot/*
cd src/Management/Jigen.Insight 
npm run build 
cd ../../..
cp -r src/Management/Jigen.Insight/dist/* publish/wwwroot

podman build . -t ppossanzini/jigendb:latest -t ppossanzini/jigendb:$Version

podman login
podman push ppossanzini/jigendb:latest
podman push ppossanzini/jigendb:$Version
