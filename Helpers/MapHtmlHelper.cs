using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManutMap.Helpers
{
    public static class MapHtmlHelper
    {
        public static string GetHtml() => """
<!DOCTYPE html>
<html><head>
  <meta charset='utf-8'/>
  <title>Mapa</title>
  <link rel='stylesheet' href='https://unpkg.com/leaflet/dist/leaflet.css'/>
  <link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster/dist/MarkerCluster.css'/>
  <link rel='stylesheet' href='https://unpkg.com/leaflet.markercluster/dist/MarkerCluster.Default.css'/>
  <script src='https://unpkg.com/leaflet/dist/leaflet.js'></script>
  <script src='https://unpkg.com/leaflet.markercluster/dist/leaflet.markercluster.js'></script>
  <style>html, body, #map { height:100%; margin:0; padding:0; }</style>
</head><body>
  <div id='map'></div>
  <script>
    var map = L.map('map').setView([-8.0, -70.0], 5);
    var osm = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',{
      attribution:'&copy; OpenStreetMap contributors'
    });
    var sat = L.tileLayer(
      'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
      { attribution:'Tiles &copy; Esri' }
    );
    osm.addTo(map);
    L.control.layers({'Mapa': osm, 'Satélite': sat}).addTo(map);

    var markerGroup = L.markerClusterGroup();
    map.addLayer(markerGroup);

    function setClustering(enabled){
      if(markerGroup){
        map.removeLayer(markerGroup);
      }
      markerGroup = enabled ? L.markerClusterGroup() : L.layerGroup();
      map.addLayer(markerGroup);
    }

    function fmtDate(str){
      if(!str) return '';
      var d = new Date(str.replace(' ', 'T'));
      if(isNaN(d.getTime())){
        var m = str.match(/(\d{1,2})\/(\d{1,2})\/(\d{4})(?:\s*(\d{1,2}):(\d{1,2}))?/);
        if(m){
          var day = parseInt(m[1]), mon = parseInt(m[2])-1,
              yr = parseInt(m[3]), hh = m[4]?parseInt(m[4]):0,
              mm = m[5]?parseInt(m[5]):0;
          d = new Date(yr, mon, day, hh, mm);
        } else return str;
      }
      return ('0'+d.getDate()).slice(-2)+'/'+
             ('0'+(d.getMonth()+1)).slice(-2)+'/'+
             d.getFullYear()+' '+
             ('0'+d.getHours()).slice(-2)+':'+
             ('0'+d.getMinutes()).slice(-2);
    }

    function clearMarkers(){
      markerGroup.clearLayers();
    }

    function addMarkers(data, showOpen, showClosed, colorOpen, colorClosed, latLonField){
      clearMarkers();

      data.forEach(function(item) {
        var dtRec = item.DTAHORARECLAMACAO ? fmtDate(item.DTAHORARECLAMACAO.trim()) : '';
        var dtCon = item.DTCONCLUSAO     ? fmtDate(item.DTCONCLUSAO.trim())     : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if ((isOpen && !showOpen) || (isClosed && !showClosed)) return;
        var coord = null;
        if(latLonField === 'LATLON')
          coord = item.LATLON;
        else if(latLonField === 'LATLONCON')
          coord = item.LATLONCON;

        if (!coord || coord.trim() === '') return;

        var coordStr = coord.trim();
        var nums = coordStr.match(/-?\d+(?:[.,]\d+)?/g);
        if(!nums || nums.length < 2) return;
        var lat = parseFloat(nums[0].replace(',', '.')),
            lng = parseFloat(nums[1].replace(',', '.'));
        if(isNaN(lat) || isNaN(lng)) return;
        var color = isOpen ? colorOpen : colorClosed;

        var m = L.circleMarker([lat,lng],{
          radius:6, fillColor:color, color:'#fff', weight:1.2, fillOpacity:0.9
        });
        markerGroup.addLayer(m);

        var temDat = item.TemDatalog || item.TEMDATALOG || false;
        var datUrl = item.FolderUrl || item.FOLDERURL || '';
        var descExec = item.DESCADICIONALEXEC || '';
        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : '';
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : '';
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;
        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : '';
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : '';
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;
        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : '';
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : '';
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;

        var osField = item.NUMOS_LIST || item.NUMOS;
        var osLabel = item.NUMOS_LIST ? 'OSs' : 'OS';
        var popup = '<b>'+osLabel+':</b> '+osField+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    '<b>Prev. abertas:</b> '+item.PREV_ABERTAS_CLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> <span style="color:'+color+'">Aberto</span><br>'
                           :'<b>Status:</b> <span style="color:'+color+'">Concluído</span><br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    (item.FUNCIONARIOS?'<b>Funcionários:</b> '+item.FUNCIONARIOS+'<br>':'')+
                    '<b>Datalog:</b> '+(temDat?'Sim':'Não')+'<br>'+
                    (datUrl?'<a href="'+datUrl+'" target="_blank">Abrir Datalog</a><br>':'')+
                    (prevUlt?'<b>Última Prev.:</b> '+prevUlt+'<br>':'')+
                    (prevProx?'<b>Próxima Prev.:</b> '+prevProx+' <span style="color:'+(prevDias<=0?'IndianRed':(prevDias<=30?'Orange':'Green'))+'">('+prevDias+' dias)</span><br>':'')+
                    (corrDias!==undefined?'<b>Corretiva SLA:</b> <span style="color:'+(corrDias<=0?'IndianRed':(corrDias<=2?'Orange':'Green'))+'">'+corrDias+' dias</span><br>':'')+
                    (isClosed?'<b>DESCADICIONALEXEC:</b> '+descExec+'<br>':'')+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
      });

      if(markerGroup.getLayers().length>0){
        map.fitBounds(markerGroup.getBounds().pad(0.2));
      }
    }

    function addMarkersSelective(data, showOpen, showClosed, colorOpen, colorClosed, colorPrev, colorCorr, colorServ, doPrev, doCorr, doServ, latLonField){
      clearMarkers();

      data.forEach(function(item){
        var dtRec = item.DTAHORARECLAMACAO ? fmtDate(item.DTAHORARECLAMACAO.trim()) : '';
        var dtCon = item.DTCONCLUSAO     ? fmtDate(item.DTCONCLUSAO.trim())     : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if((isOpen && !showOpen) || (isClosed && !showClosed)) return;

        var coord = null;
        if(latLonField === 'LATLON')
          coord = item.LATLON;
        else if(latLonField === 'LATLONCON')
          coord = item.LATLONCON;
        if(!coord || coord.trim() === '') return;

        var coordStr = coord.trim();
        var nums = coordStr.match(/-?\d+(?:[.,]\d+)?/g);
        if(!nums || nums.length < 2) return;
        var lat = parseFloat(nums[0].replace(',', '.')),
            lng = parseFloat(nums[1].replace(',', '.'));
        if(isNaN(lat) || isNaN(lng)) return;

        var tipo = (item.TIPO || '').toString().trim().toLowerCase();
        var color = isOpen ? colorOpen : colorClosed;
        if(tipo === 'preventiva' && doPrev) color = colorPrev;
        else if(tipo === 'corretiva' && doCorr) color = colorCorr;
        else if(tipo !== 'preventiva' && tipo !== 'corretiva' && doServ) color = colorServ;

        var m = L.circleMarker([lat,lng],{
          radius:6, fillColor:color, color:'#fff', weight:1.2, fillOpacity:0.9
        });
        markerGroup.addLayer(m);

        var temDat = item.TemDatalog || item.TEMDATALOG || false;
        var datUrl = item.FolderUrl || item.FOLDERURL || '';
        var descExec = item.DESCADICIONALEXEC || '';
        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : "";
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : "";
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;

        var osField = item.NUMOS_LIST || item.NUMOS;
        var osLabel = item.NUMOS_LIST ? 'OSs' : 'OS';
        var popup = '<b>'+osLabel+':</b> '+osField+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    '<b>Prev. abertas:</b> '+item.PREV_ABERTAS_CLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> <span style="color:'+color+'">Aberto</span><br>'
                           :'<b>Status:</b> <span style="color:'+color+'">Concluído</span><br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    (item.FUNCIONARIOS?'<b>Funcionários:</b> '+item.FUNCIONARIOS+'<br>':'')+
                    '<b>Datalog:</b> '+(temDat?'Sim':'Não')+'<br>'+
                    (datUrl?'<a href="'+datUrl+'" target="_blank">Abrir Datalog</a><br>':'')+
                    (prevUlt?'<b>Última Prev.:</b> '+prevUlt+'<br>':'')+
                    (prevProx?'<b>Próxima Prev.:</b> '+prevProx+' <span style="color:'+(prevDias<=0?'IndianRed':(prevDias<=30?'Orange':'Green'))+'">('+prevDias+' dias)</span><br>':'')+
                    (corrDias!==undefined?'<b>Corretiva SLA:</b> <span style="color:'+(corrDias<=0?'IndianRed':(corrDias<=2?'Orange':'Green'))+'">'+corrDias+' dias</span><br>':'')+
                    (isClosed?'<b>DESCADICIONALEXEC:</b> '+descExec+'<br>':'')+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
      });

      if(markerGroup.getLayers().length>0){
        map.fitBounds(markerGroup.getBounds().pad(0.2));
      }
    }

    function addMarkersByTipoSigfi(data, showOpen, showClosed, colorPrev, colorCorr, latLonField){
      clearMarkers();

      data.forEach(function(item){
        var dtRec = item.DTAHORARECLAMACAO ? fmtDate(item.DTAHORARECLAMACAO.trim()) : '';
        var dtCon = item.DTCONCLUSAO     ? fmtDate(item.DTCONCLUSAO.trim())     : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if((isOpen && !showOpen) || (isClosed && !showClosed)) return;

        var coord = null;
        if(latLonField === 'LATLON')
          coord = item.LATLON;
        else if(latLonField === 'LATLONCON')
          coord = item.LATLONCON;
        if(!coord || coord.trim() === '') return;

        var coordStr = coord.trim();
        var nums = coordStr.match(/-?\d+(?:[.,]\d+)?/g);
        if(!nums || nums.length < 2) return;
        var lat = parseFloat(nums[0].replace(',', '.')),
            lng = parseFloat(nums[1].replace(',', '.'));
        if(isNaN(lat) || isNaN(lng)) return;

        var tipo = (item.TIPODESIGFI || '').toString().trim().toLowerCase();
        var color = tipo === 'preventiva' ? colorPrev :
                    tipo === 'corretiva' ? colorCorr : colorPrev;

        var m = L.circleMarker([lat,lng],{
          radius:6, fillColor:color, color:'#fff', weight:1.2, fillOpacity:0.9
        });
        markerGroup.addLayer(m);

        var temDat = item.TemDatalog || item.TEMDATALOG || false;
        var datUrl = item.FolderUrl || item.FOLDERURL || '';
        var descExec = item.DESCADICIONALEXEC || '';
        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : "";
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : "";
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;

        var osField = item.NUMOS_LIST || item.NUMOS;
        var osLabel = item.NUMOS_LIST ? 'OSs' : 'OS';
        var popup = '<b>'+osLabel+':</b> '+osField+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    '<b>Prev. abertas:</b> '+item.PREV_ABERTAS_CLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> <span style="color:'+color+'">Aberto</span><br>'
                           :'<b>Status:</b> <span style="color:'+color+'">Concluído</span><br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    (item.FUNCIONARIOS?'<b>Funcionários:</b> '+item.FUNCIONARIOS+'<br>':'')+
                    '<b>Datalog:</b> '+(temDat?'Sim':'Não')+'<br>'+
                    (datUrl?'<a href="'+datUrl+'" target="_blank">Abrir Datalog</a><br>':'')+
                    (prevUlt?'<b>Última Prev.:</b> '+prevUlt+'<br>':'')+
                    (prevProx?'<b>Próxima Prev.:</b> '+prevProx+' <span style="color:'+(prevDias<=0?'IndianRed':(prevDias<=30?'Orange':'Green'))+'">('+prevDias+' dias)</span><br>':'')+
                    (corrDias!==undefined?'<b>Corretiva SLA:</b> <span style="color:'+(corrDias<=0?'IndianRed':(corrDias<=2?'Orange':'Green'))+'">'+corrDias+' dias</span><br>':'')+
                    (isClosed?'<b>DESCADICIONALEXEC:</b> '+descExec+'<br>':'')+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
      });

      if(markerGroup.getLayers().length>0){
        map.fitBounds(markerGroup.getBounds().pad(0.2));
      }
    }

    function addMarkersByTipoServico(data, showOpen, showClosed, colorPrev, colorCorr, colorServ, latLonField){
      clearMarkers();

      data.forEach(function(item){
        var dtRec = item.DTAHORARECLAMACAO ? fmtDate(item.DTAHORARECLAMACAO.trim()) : '';
        var dtCon = item.DTCONCLUSAO     ? fmtDate(item.DTCONCLUSAO.trim())     : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if((isOpen && !showOpen) || (isClosed && !showClosed)) return;

        var coord = null;
        if(latLonField === 'LATLON')
          coord = item.LATLON;
        else if(latLonField === 'LATLONCON')
          coord = item.LATLONCON;
        if(!coord || coord.trim() === '') return;

        var coordStr = coord.trim();
        var nums = coordStr.match(/-?\d+(?:[.,]\d+)?/g);
        if(!nums || nums.length < 2) return;
        var lat = parseFloat(nums[0].replace(',', '.')),
            lng = parseFloat(nums[1].replace(',', '.'));
        if(isNaN(lat) || isNaN(lng)) return;

        var tipo = (item.TIPO || '').toString().trim().toLowerCase();
        var color = tipo === 'preventiva' ? colorPrev :
                    tipo === 'corretiva' ? colorCorr : colorServ;

        var m = L.circleMarker([lat,lng],{
          radius:6, fillColor:color, color:'#fff', weight:1.2, fillOpacity:0.9
        });
        markerGroup.addLayer(m);

        var temDat = item.TemDatalog || item.TEMDATALOG || false;
        var datUrl = item.FolderUrl || item.FOLDERURL || '';
        var descExec = item.DESCADICIONALEXEC || '';

        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : "";
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : "";
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;
        var osField = item.NUMOS_LIST || item.NUMOS;
        var osLabel = item.NUMOS_LIST ? 'OSs' : 'OS';
        var popup = '<b>'+osLabel+':</b> '+osField+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    '<b>Prev. abertas:</b> '+item.PREV_ABERTAS_CLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> <span style="color:'+color+'">Aberto</span><br>'
                           :'<b>Status:</b> <span style="color:'+color+'">Concluído</span><br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    (item.FUNCIONARIOS?'<b>Funcionários:</b> '+item.FUNCIONARIOS+'<br>':'')+
                    '<b>Datalog:</b> '+(temDat?'Sim':'Não')+'<br>'+
                    (datUrl?'<a href="'+datUrl+'" target="_blank">Abrir Datalog</a><br>':'')+
                    (prevUlt?'<b>Última Prev.:</b> '+prevUlt+'<br>':'')+
                    (prevProx?'<b>Próxima Prev.:</b> '+prevProx+' <span style="color:'+(prevDias<=0?'IndianRed':(prevDias<=30?'Orange':'Green'))+'">('+prevDias+' dias)</span><br>':'')+
                    (corrDias!==undefined?'<b>Corretiva SLA:</b> <span style="color:'+(corrDias<=0?'IndianRed':(corrDias<=2?'Orange':'Green'))+'">'+corrDias+' dias</span><br>':'')+
                    (isClosed?'<b>DESCADICIONALEXEC:</b> '+descExec+'<br>':'')+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
      });

      if(markerGroup.getLayers().length>0){
        map.fitBounds(markerGroup.getBounds().pad(0.2));
      }
    }

    function addMarkersByTipoServicoIcon(data, showOpen, showClosed, latLonField){
      clearMarkers();

      var iconPrev = new L.Icon({
        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-blue.png',
        shadowUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-shadow.png',
        iconSize: [25,41],
        iconAnchor: [12,41],
        popupAnchor: [1,-34],
        shadowSize: [41,41]
      });
      var iconCorr = new L.Icon({
        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
        shadowUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-shadow.png',
        iconSize: [25,41],
        iconAnchor: [12,41],
        popupAnchor: [1,-34],
        shadowSize: [41,41]
      });
      var iconServ = new L.Icon({
        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-green.png',
        shadowUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-shadow.png',
        iconSize: [25,41],
        iconAnchor: [12,41],
        popupAnchor: [1,-34],
        shadowSize: [41,41]
      });

      data.forEach(function(item){
        var dtRec = item.DTAHORARECLAMACAO ? fmtDate(item.DTAHORARECLAMACAO.trim()) : '';
        var dtCon = item.DTCONCLUSAO     ? fmtDate(item.DTCONCLUSAO.trim())     : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if((isOpen && !showOpen) || (isClosed && !showClosed)) return;

        var coord = null;
        if(latLonField === 'LATLON')
          coord = item.LATLON;
        else if(latLonField === 'LATLONCON')
          coord = item.LATLONCON;
        if(!coord || coord.trim() === '') return;

        var coordStr = coord.trim();
        var nums = coordStr.match(/-?\d+(?:[.,]\d+)?/g);
        if(!nums || nums.length < 2) return;
        var lat = parseFloat(nums[0].replace(',', '.')),
            lng = parseFloat(nums[1].replace(',', '.'));
        if(isNaN(lat) || isNaN(lng)) return;

        var tipo = (item.TIPO || '').toString().trim().toLowerCase();
        var icon = tipo === 'preventiva' ? iconPrev :
                    tipo === 'corretiva' ? iconCorr : iconServ;

        var m = L.marker([lat,lng],{ icon: icon });
        markerGroup.addLayer(m);

        var temDat = item.TemDatalog || item.TEMDATALOG || false;
        var datUrl = item.FolderUrl || item.FOLDERURL || '';
        var descExec = item.DESCADICIONALEXEC || '';

        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : "";
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : "";
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;
        var osField = item.NUMOS_LIST || item.NUMOS;
        var osLabel = item.NUMOS_LIST ? 'OSs' : 'OS';
        var popup = '<b>'+osLabel+':</b> '+osField+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    '<b>Prev. abertas:</b> '+item.PREV_ABERTAS_CLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> Aberto<br>' : '<b>Status:</b> Concluído<br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    (item.FUNCIONARIOS?'<b>Funcionários:</b> '+item.FUNCIONARIOS+'<br>':'')+
                    '<b>Datalog:</b> '+(temDat?'Sim':'Não')+'<br>'+
                    (datUrl?'<a href="'+datUrl+'" target="_blank">Abrir Datalog</a><br>':'')+
                    (prevUlt?'<b>Última Prev.:</b> '+prevUlt+'<br>':'')+
                    (prevProx?'<b>Próxima Prev.:</b> '+prevProx+' <span style="color:'+(prevDias<=0?'IndianRed':(prevDias<=30?'Orange':'Green'))+'">('+prevDias+' dias)</span><br>':'')+
                    (corrDias!==undefined?'<b>Corretiva SLA:</b> <span style="color:'+(corrDias<=0?'IndianRed':(corrDias<=2?'Orange':'Green'))+'">'+corrDias+' dias</span><br>':'')+
                    (isClosed?'<b>DESCADICIONALEXEC:</b> '+descExec+'<br>':'')+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
      });

      if(markerGroup.getLayers().length>0){
        map.fitBounds(markerGroup.getBounds().pad(0.2));
      }
    }

    function addMarkersCustomIcon(data, showOpen, showClosed, iconUrl, latLonField){
      clearMarkers();

      var icon = new L.Icon({
        iconUrl: iconUrl,
        shadowUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-shadow.png',
        iconSize: [25,41],
        iconAnchor: [12,41],
        popupAnchor: [1,-34],
        shadowSize: [41,41]
      });

      data.forEach(function(item){
        var dtRec = item.DTAHORARECLAMACAO ? fmtDate(item.DTAHORARECLAMACAO.trim()) : '';
        var dtCon = item.DTCONCLUSAO     ? fmtDate(item.DTCONCLUSAO.trim()) : '';
        var isOpen   = dtRec !== '' && dtCon === '';
        var isClosed = dtCon !== '';
        if((isOpen && !showOpen) || (isClosed && !showClosed)) return;

        var coord = null;
        if(latLonField === 'LATLON')
          coord = item.LATLON;
        else if(latLonField === 'LATLONCON')
          coord = item.LATLONCON;
        if(!coord || coord.trim() === '') return;

        var coordStr = coord.trim();
        var nums = coordStr.match(/-?\d+(?:[.,]\d+)?/g);
        if(!nums || nums.length < 2) return;
        var lat = parseFloat(nums[0].replace(',', '.')),
            lng = parseFloat(nums[1].replace(',', '.'));
        if(isNaN(lat) || isNaN(lng)) return;

        var m = L.marker([lat,lng],{ icon: icon });
        markerGroup.addLayer(m);

        var temDat = item.TemDatalog || item.TEMDATALOG || false;
        var datUrl = item.FolderUrl || item.FOLDERURL || '';
        var descExec = item.DESCADICIONALEXEC || '';
        var prevUlt = item.PREV_ULTIMA ? fmtDate(item.PREV_ULTIMA) : "";
        var prevProx = item.PREV_PROXIMA ? fmtDate(item.PREV_PROXIMA) : "";
        var prevDias = item.PREV_DIAS;
        var corrDias = item.CORR_DIAS;

        var osField = item.NUMOS_LIST || item.NUMOS;
        var osLabel = item.NUMOS_LIST ? 'OSs' : 'OS';
        var popup = '<b>'+osLabel+':</b> '+osField+'<br>'+
                    '<b>Cliente:</b> '+item.NOMECLIENTE+'<br>'+
                    '<b>Prev. abertas:</b> '+item.PREV_ABERTAS_CLIENTE+'<br>'+
                    (isOpen?'<b>Status:</b> Aberto<br>' : '<b>Status:</b> Concluído<br>')+
                    (dtRec?'<b>Abertura:</b> '+dtRec+'<br>':'')+
                    (dtCon?'<b>Conclusão:</b> '+dtCon+'<br>':'')+
                    '<b>Rota:</b> '+item.ROTA+'<br>'+
                    '<b>Tipo SIGFI:</b> '+item.TIPODESIGFI+'<br>'+
                    '<b>IDSIGFI:</b> '+item.IDSIGFI+'<br>'+
                    '<b>Serviço:</b> '+item.TIPO+'<br>'+
                    (item.FUNCIONARIOS?'<b>Funcionários:</b> '+item.FUNCIONARIOS+'<br>':'')+
                    '<b>Datalog:</b> '+(temDat?'Sim':'Não')+'<br>'+
                    (datUrl?'<a href="'+datUrl+'" target="_blank">Abrir Datalog</a><br>':'')+
                    (prevUlt?'<b>Última Prev.:</b> '+prevUlt+'<br>':'')+
                    (prevProx?'<b>Próxima Prev.:</b> '+prevProx+' <span style="color:'+(prevDias<=0?'IndianRed':(prevDias<=30?'Orange':'Green'))+'">('+prevDias+' dias)</span><br>':'')+
                    (corrDias!==undefined?'<b>Corretiva SLA:</b> <span style="color:'+(corrDias<=0?'IndianRed':(corrDias<=2?'Orange':'Green'))+'">'+corrDias+' dias</span><br>':'')+
                    (isClosed?'<b>DESCADICIONALEXEC:</b> '+descExec+'<br>':'')+
                    '<b>LatLon ('+latLonField+'):</b> '+coordStr;

        m.bindPopup(popup);
      });

      if(markerGroup.getLayers().length>0){
        map.fitBounds(markerGroup.getBounds().pad(0.2));
      }
    }
    </script>
</body></html>

""";

        public static string GetHtmlWithData(IEnumerable<JObject> data,
                                             bool showOpen,
                                             bool showClosed,
                                             string colorOpen,
                                             string colorClosed,
                                             string colorPrev,
                                             string colorCorr,
                                             string colorServ,
                                             bool colorPrevOn,
                                             bool colorCorrOn,
                                             bool colorServOn,
                                             string latLonField,
                                             bool iconByTipo = false,
                                             string? customIcon = null,
                                             bool useClusters = true)
        {
            var baseHtml = GetHtml();
            var json = JsonConvert.SerializeObject(data);
            string call;
            if(customIcon != null)
                call = $"addMarkersCustomIcon(data,{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()},'{customIcon}','{latLonField}');";
            else if(iconByTipo)
                call = $"addMarkersByTipoServicoIcon(data,{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()},'{latLonField}');";
            else
                call = $"addMarkersSelective(data,{showOpen.ToString().ToLower()},{showClosed.ToString().ToLower()},'{colorOpen}','{colorClosed}','{colorPrev}','{colorCorr}','{colorServ}',{colorPrevOn.ToString().ToLower()},{colorCorrOn.ToString().ToLower()},{colorServOn.ToString().ToLower()},'{latLonField}');";
            var script = $"<script>var data = {json};window.onload=function(){{setClustering({useClusters.ToString().ToLower()});{call}}}</script>";
            return baseHtml.Replace("</body></html>", script + "</body></html>");
        }
    }
}
