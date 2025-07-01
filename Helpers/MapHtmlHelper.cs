namespace ManutMap.Helpers
{
    public static class MapHtmlHelper
    {
        public static string GetHtml() => @"
<!DOCTYPE html>
<html><head>
  <meta charset='utf-8'/>
  <title>Mapa</title>
  <link rel='stylesheet' href='https://unpkg.com/leaflet/dist/leaflet.css'/>
  <script src='https://unpkg.com/leaflet/dist/leaflet.js'></script>
  <style>html, body, #map { height:100%; margin:0; padding:0; }</style>
</head><body>
  <div id='map'></div>
  <script>
    var map = L.map('map').setView([-8.0, -70.0], 5);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{
      attribution:'&copy; OpenStreetMap contributors'
    }).addTo(map);

    var markers = [];
    function clearMarkers(){
      markers.forEach(m=>map.removeLayer(m));
      markers = [];
    }
    // Agora recebe um argumento a mais: latLonField
    function addMarkers(data, showOpen, showClosed, colorOpen, colorClosed, latLonField){
      clearMarkers();

      data.forEach(function(item) {
        var dtRec = item.DTAHORARECLAMACAO ? item.DTAHORARECLAMACAO.trim() : '';
        var dtCon = item.DTCONCLUSAO     ? item.DTCONCLUSAO.trim()     : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if ((isOpen && !showOpen) || (isClosed && !showClosed)) return;
        var coord = item.LATLON;
        if (!coord || coord.trim() === '') coord = item.LATLONCON;
        if (!coord || coord.trim() === '') return;
        var parts = coord.split(',');
        var lat = parseFloat(parts[0]), lng = parseFloat(parts[1]);
        if(isNaN(lat)||isNaN(lng))return;
        var color = isOpen?colorOpen:colorClosed;

        var m = L.circleMarker([lat,lng],{
          radius:6, fillColor:color, color:'#fff', weight:1.2, fillOpacity:0.9
        }).addTo(map);

        var popup = '<b>OS:</b> '+item.NUMOS+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> <span style=""color:'+color+'"">Aberto</span><br>'
                           :'<b>Status:</b> <span style=""color:'+color+'"">Concluído</span><br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
        markers.push(m);
      });
      if(markers.length>0){
        var grp = L.featureGroup(markers);
        map.fitBounds(grp.getBounds().pad(0.2));
      }
    }
  </script>
</body></html>";
    }
}
