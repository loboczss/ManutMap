namespace ManutMap.Helpers
{
    public static class MapHtmlHelper
    {
        public static string GetHtml() => @"
<!DOCTYPE html>
<html>
<head>
  <meta charset='utf-8'/>
  <title>Mapa</title>
  <link rel='stylesheet' href='https://unpkg.com/leaflet/dist/leaflet.css'/>
  <script src='https://unpkg.com/leaflet/dist/leaflet.js'></script>
  <style>html, body, #map { height:100%; margin:0; padding:0; }</style>
</head>
<body>
  <div id='map'></div>
  <script>
    var map = L.map('map').setView([-8.0, -70.0], 5);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);
    var markers = [];
    function clearMarkers() {
      markers.forEach(m => map.removeLayer(m));
      markers = [];
    }
    function addMarkers(data, showOpen, showClosed, colorOpen, colorClosed) {
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
        if (isNaN(lat) || isNaN(lng)) return;
        var color = isOpen ? colorOpen : colorClosed;
        var m = L.circleMarker([lat, lng], {
          radius: 6, fillColor: color, color:'#fff', weight:1.2, fillOpacity:0.9
        }).addTo(map);
        var popup = '<b>' + item.NUMOS + '</b><br>' +
                    item.NOMECLIENTE + '<br>' +
                    (isOpen
                      ? '<span style=""color:' + color + '"">Aberto</span>'
                      : '<span style=""color:' + color + '"">Concluído</span>');
        if (dtRec) popup += '<br><b>Abertura:</b> ' + dtRec;
        if (dtCon) popup += '<br><b>Conclusão:</b> ' + dtCon;
        m.bindPopup(popup);
        markers.push(m);
      });
      if (markers.length > 0) {
        var group = L.featureGroup(markers);
        map.fitBounds(group.getBounds().pad(0.2));
      }
    }
  </script>
</body>
</html>";
    }
}
