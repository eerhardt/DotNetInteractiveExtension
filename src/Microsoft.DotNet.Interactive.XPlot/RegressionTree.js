var dnRegressionTree = (function () {
    const blockHeight = 60;
    const blockWidth = 100;
    const dotSize = 10;

    function renderRegressionTree(renderTarget, regressionTree, d3) {

        //************************************* Options******************************************************//

        const tree_branch = false; // if the thickness of the branches depend on the value of targt + color * /
        const tree_branch_parent = true; // true: thickness from the root if not the direct parent
        const tree_branch_color = "black";
        const strokeness = 120; // the degree of separation between the nodes 
        const default_strokeness = 50;
        const hover_percent_parent = false; // if the display percentage depends on the direct parent or the root
        const square = false;
        const rect_percent = true; //display the percentage or the value in the small rectangles of the labels 
        const value_percent_top = true; /// if we display the value and the percentage above the rectangle /

        const dict_leaf_y = { 1: 0, 2: -17.5, 3: -35, 4: -52.5, 5: -70, 6: -87.5, 6: -105, 7: -122.5, 8: -140, 9: -157.5, 10: -175 };

        let label_names;

        let TOTAL_SIZE;
        let default_colors = [
            "#c25975", "#d26bff", "#2d5a47", "#093868", "#fcdfe6", "#94a2fa", "#faec94", "#decaee", "#daeeca", "#b54c0a", "#dc1818", "#18dcdc", "#000000", "#340000", "#86194c", "#fef65b", "#ff9b6f", "#491b47", "#171717", "#e8efec", "#1c6047", "#a2bae0", "#4978c3", "#f8fee0", "#dcfb66", "#91fb66", "#29663b", "#b4b7be", "#0088b2", "#88b200", "#c43210", "#f06848", "#f0bc48", "#d293a2", "#cccccc", "#59596a", "#fafae6", "#ffc125", "#ff4e50", "#f0e6fa", "#f6c1c3", "#363636"
        ];
        /****************************************************************************************************** */

        let margin = { top: 20, right: 120, bottom: 20, left: 180 };
        let width = 2000 + 960 - margin.right - margin.left;
        let height = 800 - margin.top - margin.bottom;

        let root = d3.hierarchy(regressionTree);

        renderTarget
            .attr("width", getDepth(root) * width / 8 + margin.right + margin.left)
            .attr("height", height + margin.top + margin.bottom)
            .append("g")
            .attr("class", "rootTransform")
            .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

        let rootTransform = renderTarget.select("g");

        let toolTip = createToolTip(renderTarget);

        let treeLayout = d3.tree()
            .separation((a, b) => ((a.parent == root) && (b.parent == root)) ? strokeness : strokeness)
            .size([height, getDepth(root) * (blockWidth * 1.8)]);


        root.dx = blockHeight / 2;
        root.dy = blockWidth * 1.5;

        let id = 0;
        root.eachBefore(c => c.id = id++);

        rootTransform
            .append("g")
            .attr("class", "linkLayer");

        rootTransform.append("g")
            .attr("class", "nodeLayer")
            .attr("stroke-linejoin", "round")
            .attr("stroke-width", 3);

        //root.children.forEach(collapse);
        update(root, rootTransform, treeLayout);
    }

    function collapse(d) {
        if (d.children) {
            d._children = d.children;
            d._children.forEach(collapse);
            d.children = null;
        }
    }

    function expand(d) {
        if (d._children) {
            d.children = d._children;
            d.children.forEach(expand);
            d._children = null;
        }
    }


    function toggleChildren(d) {
        if (d.children) {
            d._children = d.children;
            d.children = null;
        } else if (d._children) {
            d.children = d._children;
            d._children = null;
        }
        return d;
    }


    function getDepth(treeNode) {
        let depth = 0;
        if (treeNode.children) {
            treeNode.children.forEach((d) => {
                var tmpDepth = getDepth(d);
                if (tmpDepth > depth) {
                    depth = tmpDepth;
                }
            });
        }
        return 1 + depth;
    }

    function createToolTip(renderTarget) {

    }

    function rightRoundedRect(x, y, width, height, radius) {
        return "M" + x + "," + y
            + "h" + (width - radius)
            + "a" + radius + "," + radius + " 0 0 1 " + radius + "," + radius
            + "v" + (height - 2 * radius)
            + "a" + radius + "," + radius + " 0 0 1 " + -radius + "," + radius
            + "h" + (radius - width)
            + "z";
    }

    function updateLinks(root, renderTarget) {
        let offset = blockWidth;
        let internalOffset = root.dy;

        let link = renderTarget
            .select("g.linkLayer")
            .attr("fill", "none")
            .attr("stroke", "#555")
            .attr("stroke-opacity", 0.4)
            .attr("stroke-width", 1.5)
            .selectAll("path")
            .data(root.links(), d => `${d.source.id}_${d.target.id}`)
            .join("path")
            .attr("d", d => {
                return `
        M${d.target.y},${d.target.x}
        C${d.source.y + internalOffset},${d.target.x}
         ${d.source.y + internalOffset},${d.source.x}
         ${d.source.y + offset},${d.source.x}
      `;
            });
    }

    function updateNodes(root, renderTarget, treeLayout) {
        let node = renderTarget
            .select("g.nodeLayer")
            .selectAll("g.nodeRootTransform")
            .data(root.descendants(), d => d.id)
            .join("g")
            .attr("class", "nodeRootTransform")
            .attr("transform", d => `translate(${d.y + blockWidth / 2},${d.x})`);

        // root
        node
            .append("g")
            .attr("class", "nodeExpander")
            .attr("transform", d => `translate(${blockWidth / 2},0)`)
            .append("circle")
            .attr("strong", "black")
            .attr("fill", d => d.children ? "#555" : "#999")
            .attr("r", d => (d.children || d._children) ? dotSize : 0)
            .on("click", d => {
                toggleChildren(d);
                update(root, renderTarget, treeLayout);
            });


        let strokeSize = 1;
        let boxX = -(blockWidth / 2);
        let boxy = -(blockHeight / 2);
        let boxW = blockWidth;
        let boxH = blockHeight;
        let boxR = (blockHeight / 4);

        // node block
        node
            .append("path")
            .attr("class", "nodeBlock")
            .attr("d", rightRoundedRect(boxX, boxy, boxW, boxH, boxR))
            .attr("fill", "white")
            .attr("stroke", "black");


        // data flow part
        node
            .append("path")
            .attr("class", "dataFlow")
            .attr("d", rightRoundedRect(boxX + strokeSize, boxy + strokeSize, boxW - (2 * strokeSize), boxH - (2 * strokeSize), boxR))
            .attr("fill", "teal")
            .attr("style", d => `clip-path: inset( 0% 0% 0% ${(1 - d.data.data) * 100}% );`);

        node
            .append("text")
            .attr("class", "nodeText")
            .attr("dy", "0.31em")
            .text(d => d.data.label ? d.data.label : d.data.value)
            .filter(d => d.children)
            .attr("text-anchor", "end")
            .clone(true)
            .lower()
            .attr("stroke", "white");
    }
    function update(root, renderTarget, treeLayout) {
        treeLayout(root);
        updateLinks(root, renderTarget);
        updateNodes(root, renderTarget, treeLayout);
    }

    return {
        render: renderRegressionTree
    };
})();
