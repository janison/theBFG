function randfloor(min, max) { // min and max included 
    return Math.floor(Math.random() * (max - min + 1) + min);
  }

angular.module('portal')

  .directive('barChart', function() {
    return {
      restrict: 'E',
      scope: { 
        data: '='
      },
      link: function(scope, element) {
        var canvasWidth = document.body.clientWidth / 3;
        var height = element.attr("height");
        
        var x = d3.scaleLinear()
            .domain([0, 100])
            .range([0, canvasWidth]);
        // init
        var canvas = d3.select(element[0])
            .append('svg')            
            .attr('height', height)
            .attr('width', canvasWidth)          
            .append('g')           ;           
            
        var fix = function(e) {
            return e == "<" ? "1" : e;
        };        

        
        //var x = d3.scaleLinear()
        //.domain([0, 100])
        //.range([0, canvasWidth]);

        //scope.$watch('maxDuration', function(maxDuration) {
        //  console.info("Setting threashold not working: "+maxDuration);
        //  // x = d3.scaleLinear()
        //  // .domain([0, maxDuration])
        //  // .range([0, canvasWidth]);
        //});
        
        // update
        scope.$watch('data', function(dataArray) {
            var update = canvas.selectAll("g").data(dataArray);

            
            var enter = update.enter().append("g");
            enter.append("rect")
                
            var bars = update.merge(enter);

            //Update rectangles
            bars
                .select("rect")
                .attr("class", function (d, i) { return !d.result ? "inprogress" : d.result === 'Passed' ?  'infor' : 'error'})
                .attr("height", function (d, i) { return ((fix(d.duration ?? "1") ) * 10) })
                .attr("width", 5)    
                .attr("title",function (d, i) { d.info ?? "" })    
                .attr("x", function (d, i) { 
                    var v = x(i);
                    return v; 
                })
                .attr("y", function (d, i) { return height - (fix(d.duration ?? "1") * 10) })
       
       
            //Exit rectangles   	
            update.exit()
                .remove();

            }, true);
      },
    };
  });


  angular.module('portal')

  .directive('pieChart', function() {
    return {
      restrict: 'E',
      scope: { data: '='},
      link: function(scope, element) {

        var width = 250; 
        var height = 250;
        var radius = Math.min(width, height) / 2;
        var arc;
        var svg;
        var pie;
        var labelArc;

        const color = function(result) {
            return result == "Passed" ? "green" : result === "Failed" ? "red" : "grey";
        }

        const nameIfLarge = function(data) {
            return parseInt(data.duration) > 100 ? data.testName : undefined; 
        }
      
    
      const initSvg = function() {
  
        arc = d3.arc()
            .outerRadius(radius - 10)
            .innerRadius(0);

        labelArc = d3.arc()
            .outerRadius(radius - 40)
            .innerRadius(radius - 40);
    
        labelPer = d3.arc()
            .outerRadius(radius - 80)
            .innerRadius(radius - 80);
    
        pie = d3.pie()
            .sort(null)
            .value(d => 1);
    
        svg = d3.select(element[0])
            .append('svg')
            .attr('width', width)
            .attr('height', height)
            .attr('viewBox', '0 0 ' + Math.min(width, height) + ' ' + Math.min(width, height))
            .append('g')
            .attr('transform', 'translate(' + Math.min(width, height) / 2 + ',' + Math.min(width, height) / 2 + ')');
      }
    
      drawPie = function(data) {
        const g = svg.selectAll('.arc')
            .data(pie(data))
            .enter().append('g')
            .attr('class', 'arc');

        g.append('path').attr('d', arc)
            .style('fill', d => color(d.data.result) );

        g.append('text').attr('transform', d => 'translate(' + labelArc.centroid(d) + ')')
            .attr('dy', '.35em')
            .text(d =>  nameIfLarge(d.data));
      }

      
      initSvg();

      
      scope.$watch('data', function(data) {
        drawPie(data);
      }, true);
        
      },
    };
  })

  .directive('workerStatus', function() {
    return {
      restrict: 'E',
      scope: { data: '=' },
      link: function(scope, element) {
        var canvasWidth = document.body.clientWidth / 3;
        var height = 80;

        
        const color = function(result) {
          return result == "Passed" ? "green" : result === "Failed" ? "red" : "grey";
      }

      //   var x = d3.scaleLinear()
      //       .domain([0, 100])
      //       .range([0, canvasWidth]);

      //   var y = d3.scaleLinear()
      //       .domain([0, 20])
      //       .range([200, 0]);
      
      //   // init
        var canvas = d3.select(element[0])
            .append('svg')            
            .attr('height', height)
            .attr('width', canvasWidth)  
            .append('g')           ;           
            
      //   var fix = function(e) {
      //       return e === "<" ? "1" : e;
      //   };        

        
      //   var line = d3.line()
      //   .curve(d3.curveBasis)
      //   .x(function(d, i) {
      //     return x(i);
      //   })
      //   .y(function(d, i) {
      //     return y(d.values);
      //   });
  
      // var area = d3.area()
      //   .curve(d3.curveBasis)
      //   .x(line.x())
      //   .y1(line.y())
      //   .y0(y(0));

    // var canvas = d3.select('#disp')
    //   .append('svg')
    //   .attr('width', 400)
    //   .attr('height', 200);

    var x = d3.scaleLinear()
      .domain([0, 100])
      .range([0, 700]);

    var y = d3.scaleLinear()
      .domain([0, 100])
      .range([80, 0]);

      
        var line = d3.line()
        .curve(d3.curveBasis)
        .x(function(d, i) {
          return x(i);
        })
        .y(function(d, i) {
          return y(d);
        });

      var area = d3.area()
        .curve(d3.curveBasis)
        .x(line.x())
        .y1(line.y())
        .y0(y(0));


 
        // update
        scope.$watch('data', function(dataArray) {            
     
          // v.push(23);
          // dataArray[0].values = v;
          // console.log("sda:"+ JSON.stringify(dataArray[0]));

              //  Not handled: {"memUsage":2070.5103,"handles":974,"threads":67,"reporterName":null,"timeCaptured":"2021-03-22T23:15:51.8399721+11:00"} 4 app.full.min.js:82015:21
              // Not handled: {"memUsage":2070.5103,"handles":974,"threads":67,"reporterName":null,"timeCaptured":"2021-03-22T23:15:52.8397176+11:00"}
        
         
              //Exit rectangles   	
              
                  //return; //comment out and below works. needs to find out how to add to graph properly
                  //its not adding to the right parents rows in the svg
                  
              var lines = canvas.selectAll('.category')
              .data(dataArray, function(d) {
                return d.category;
              });
        
              
                           
                
              
              lines
              .remove();

              lines = canvas.selectAll('.category')
              .data(dataArray, function(d) {
                return d.category;
              });
        
            // on enter append a g to hold our 3 parts
            var lE = lines.enter()
              .append('g')
              .attr('class', 'category')

            var bars = lines.merge(lE);
            
            // append a path that's our solid line on top of the area
            bars.append("path")
              .attr('class', 'lline')
              .attr("d", function(d) {
                return line(d.values);
              })
              .style("stroke", function(d) {
                return d.category;
              })
              
            //apend a path that's our filled area
            bars.append("path")
              .attr("class", "larea")
              .style("fill", function(d) {
                return d.category;
              })
              .attr("d", function(d) {
                return area(d.values);
              });
            
            // create a subselection for our "dots"
            // and on enter append a bunch of circles
            bars.selectAll(".ldot")
              .data(function(d){
                return d.values
              })
              .enter()
              .append("circle")
              .attr("r", 3)
              .attr("cx", function(d,i){
                return x(i);
              })
              .attr("cy", function(d){
                return y(d);
              })
              .attr("fill", function(d){
                return d3.select(this.parentNode).datum().category;
              });

              // Update the line paths
                
              return;
              
        var vs = [10, 50, 80, 20, 8, 80, 100, 20, 0]
        var data = ["a"]
              var update = canvas.selectAll('.category')
                .data(data);
          
              // on enter append a g to hold our 3 parts
              var enter = update.enter()
                .append('g')
                .attr('class', 'category')

                    
                var bars = update.merge(enter);
    
             
              // append a path that's our solid line on top of the area
              bars.append("path")
                .attr('class', 'lline')
                .attr("d", function(d) {
                  return line(vs);
                })
                .style("stroke", function(d) {
                  return color("Passed");
                })
                
              //apend a path that's our filled area
              // bars.append("path")
              //   .attr("class", "larea")
              //   .style("fill", function(d) {
              //     return color("Passed");
              //   })
              //   .attr("d", function(d) {
              //     return area(d.values);
              //   });
              
              // // create a subselection for our "dots"
              // // and on enter append a bunch of circles
              // bars.selectAll(".ldot")
              //   .data(function(d){
              //     return d.values
              //   })
              //   .enter()
              //   .append("circle")
              //   .attr("r", 3)
              //   .attr("cx", function(d,i){
              //     return x(i);
              //   })
              //   .attr("cy", function(d){
              //     return y(d);
              //   })
              //   .attr("fill", function(d){
              //     return color("Passed"); //d3.select(this.parentNode).datum().;
              //   });

                
                //Exit rectangles   	
                update.exit()
                    .remove();
              

            }, true);
      },
    };
  });



