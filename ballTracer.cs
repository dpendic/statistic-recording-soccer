datablock ProjectileData(ghostSoccerBallProjectile : soccerBallProjectile) {
	projectileShapeName = "base/data/shapes/empty.dts";
	uiname = "Ghost Soccer Ball";
};

datablock StaticShapeData(SoccerBallShape)
{
    shapeFile = "Add-Ons/Item_Sports/soccerBall.dts";
    //base scale of shape is .2 .2 .2
};


//Functions:
//Packaged:
//	Projectile::onAdd
//	SoccerBallProjectile::onCollision
//Created:
//	startSoccerTracer //straight projectile tracking
//	soccerTracerLoop
//
//	cullGlobalSoccerTracers
//	initSoccerRaycastTracerLoop //modeled projectile tracking using raycasts
//	soccerRaycastTracerLoop
//	clearLines
//	serverCmdClearLines
//
//	StaticShape::showToClient
//	StaticShape::showToAllClients
//	StaticShape::hideFromClient
//	StaticShape::hideFromAllClients
//	SimSet::displayTracers


if (!isObject(GlobalSoccerTracerSet)) {
	new SimSet(GlobalSoccerTracerSet) {};
}

package GBFL_SoccerBallTracer {
	function Projectile::onAdd(%proj) {
		if (%proj.getDatablock().getID() == ghostSoccerBallProjectile.getID()) {
		} else if (%proj.getDatablock().getID() == soccerBallProjectile.getID()) {
			// %ghost = new Projectile(ghostBalls) {
			// 	datablock = ghostSoccerBallProjectile;
			// 	initialPosition = %proj.getPosition();
			// 	initialVelocity = %proj.getVelocity();
			// 	sourceObj = %proj.sourceObj;
			// 	client = %proj.client;
			// 	scale = %proj.getScale();
			// };
			// MissionCleanup.add(%ghost);
			// talk("Created ghost ball...");
			%proj.tracerColor = %color = getRandom() SPC getRandom() SPC getRandom() @ " 1";
			initSoccerRaycastTracerLoop(%proj);
			// startSoccerTracer(%proj, "1 1 1 0.5");
		}
		return parent::onAdd(%proj);
	}

	function SoccerBallProjectile::onCollision(%db, %proj, %hit, %scale, %pos, %norm) {
		if (%hit.getClassName() $= "Player" || %hit.getClassName() $= "AIPlayer" || %hit.getClassName() $= "fxDTSBrick") {
			%ret = parent::onCollision(%db, %proj, %hit, %scale, %pos, %norm);
			schedule(1, %proj, initSoccerRaycastTracerLoop, %proj, %hit, 1);
			// talk(%proj.getVelocity());
			// talk(%proj.getPosition());
			return %ret;
		}
		return parent::onCollision(%db, %proj, %hit, %scale, %pos, %norm);
	}
};
activatePackage(GBFL_SoccerBallTracer);

function startSoccerTracer(%proj, %color) {
	cancel(%proj.soccerTracerLoop);
	if (!isObject(%proj.tracerSet)) {
		%proj.tracerSet = new SimSet(SoccerTracers) {};
	} else {
		%proj.tracerSet.deleteAll();
	}

	%proj.lastPos = %proj.getPosition();
	%proj.tracerColor = %color;
	soccerTracerLoop(%proj, %color);
	soccerRaycastTracerLoop(%proj.getPosition(), %proj.getVelocity(), "1 0 0 0.5");
}

function soccerTracerLoop(%proj, %color) {
	cancel(%proj.soccerTracerLoop);

	%currPos = %proj.getPosition();
	if (vectorLen(vectorSub(%currPos, %proj.lastPos)) > 0.2) {
		%proj.tracerSet.add(drawLine(%currPos, %proj.lastPos, %color, 0.05));
		%proj.lastPos = %currPos;
	}

	%proj.soccerTracerLoop = schedule(100, %proj, soccerTracerLoop, %proj, %color);
	// talk(getWord(%proj.getVelocity(), 2));
}


////////////////////


$MAXTRACERS = 18;
function cullGlobalSoccerTracers() {
	if (!isObject(GlobalSoccerTracerSet)) {
		return;
	}

	%max = $MAXTRACERS;
	if ((%ct = GlobalSoccerTracerSet.getCount()) > %max) {
		%simSet = GlobalSoccerTracerSet.getObject(%ct - %max - 1);
		%startSimColor = %simSet.color;
		while (%count < 10 && %simSet.color $= %startSimColor) {
			%simSet.deleteAll();
			// for (%i = 0; %i < %simSet.getCount(); %i++) {
				// (%shape = %simSet.getObject(%i)).hideNode("ALL");
				// if (%shape.getShapeName() !$= "") {
				// 	%shape.originalShapeName = %shape.getShapeName();
				// 	%shape.setShapeName("");
				// }
			// }
			%simSet = GlobalSoccerTracerSet.getObject(%ct - %max + %count);
			%count++;
		}
	}
}

function initSoccerRaycastTracerLoop(%proj, %hit, %bounce) {
	%simSet = new SimSet(soccerTracers) { 
		color = %proj.tracerColor; 
		tracerID = getStat("NumSoccerTracers") + 0;
		tracerNum = 0;
		displayedTracers = 0;
	};
	incStat("NumSoccerTracers", 1);
	GlobalSoccerTracerSet.add(%simSet);
	cullGlobalSoccerTracers();

	if (!%bounce) {
		%playerShape = createBoxAt(%proj.getPosition(), "0 0 0 0.5", 1);
		%playerShape.setDatablock(SoccerBallShape.getID());
		%playerShape.setNodeColor("ALL", "0 1 0 0.5");
		%playerShape.setNetFlag(6, 1);
		if (isObject(%proj.client)) {
			%playerShape.setShapeName("KCK: " @ %proj.client.name);
			%playerShape.setShapeNameColor("0 1 0");
		}
		%simSet.add(%playerShape);

		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "BALL" TAB %proj.getTransform() TAB %playerShape.getShapeName() TAB %playerShape.getShapeNameColor());
		%simSet.tracerNum++;
	}
	setStat("SoccerTracer" @ %simSet.tracerID @ "_Color", %proj.tracerColor);

	%pos = %proj.getPosition();
	%adjust = "0 0 0";
	%finalPos = vectorAdd(%pos, %adjust);

	soccerRaycastTracerLoop(%finalPos, %proj.getVelocity(), %proj.tracerColor, %hit, 0, %simSet);
}

$mod = -10.1;
function soccerRaycastTracerLoop(%pos, %vel, %color, %ignore, %count, %simSet) {
	if (%count > 10000 || !isObject(%simSet)) {
		return;
	}

	%nextVel = vectorAdd(%vel, "0 0 " @ ($mod * 0.032));
	%nextPos = vectorAdd(%pos, vectorAdd(vectorScale(%vel, 0.032), vectorScale(vectorNormalize(%vel), 0.3)));
	if (vectorLen(%nextVel) > 200) {
		%nextVel = vectorScale(vectorNormalize(%nextVel), 200);
	}
	//check for players hit
	%ray = containerRaycast(%pos, %nextPos, $TypeMasks::PlayerObjectType, %ignore);
	if (isObject(getWord(%ray, 0))) {
		%loc = getWords(%ray, 1, 3);
		%playerShape = createBoxAt(%loc, "0 0 0 0.5", 1);
		%playerShape.setDatablock(SoccerBallShape.getID());
		%playerShape.setNodeColor("ALL", "1 0 0 0.5");
		%playerShape.setNetFlag(6, 1);
		%simSet.add(%playerShape);
		%ignore = getWord(%ray, 0);
		if (isObject(%cl = getWord(%ray, 0).client)) {
			%playerShape.setShapeName("INT: " @ %cl.name);
		} else {
			%playerShape.setShapeName("INT");
		}
		%playerShape.setShapeNameColor("1 0 0");

		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "BALL" TAB %loc TAB %playerShape.getShapeName() TAB %playerShape.getShapeNameColor());
		%simSet.tracerNum++;
	}
	//check for bricks hit
	%ray = containerRaycast(%pos, %nextPos, $TypeMasks::fxBrickObjectType | $TypeMasks::TerrainObjectType, %ignore);
	if (isObject(getWord(%ray, 0))) {
		%loc = getWords(%ray, 1, 3);
		%brickShape = createBoxAt(%loc, "0 0 0 0.5", 1);
		%brickShape.setDatablock(SoccerBallShape.getID());
		%brickShape.setNodeColor("ALL", "0 0 1 0.5");
		%brickShape.setNetFlag(6, 1);
		%line = drawLine(%pos, %loc, %color, 0.05);
		%line.setNetFlag(6, 1);
		%simSet.add(%line);
		%simSet.add(%brickShape);

		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "BALL" TAB %loc TAB %brickShape.getShapeName() TAB %brickShape.getShapeNameColor());
		%simSet.tracerNum++;
		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "LINE" TAB %pos TAB %loc);
		%simSet.tracerNum++;

		%simSet.DisplayTracers();
		return;
	}

	//check if too close to ground
	//remove %ignore for proximity checks
	//drawline(%nextpos, vectorAdd(%nextPos, "0 0 -0.5"), "1 1 1 0.5", 0.01);
	if (isObject(%ignore) && %ignore.getClassName() $= "fxDTSBrick") {
		%ignore = 0;
	}

	%ray = containerRaycast(%pos, vectorAdd(%pos, "0 0 -0.35"), $TypeMasks::fxBrickObjectType | $TypeMasks::TerrainObjectType | $TypeMasks::PlayerObjectType, %ignore);
	if (isObject(getWord(%ray, 0))) {
		%loc = getWords(%ray, 1, 3);
		%upperShape = createBoxAt(%loc, "0 0 0 0.5", 1);
		%upperShape.setDatablock(SoccerBallShape.getID());
		%upperShape.setNodeColor("ALL", "1 1 1 0.5");
		%upperShape.setNetFlag(6, 1);
		%line = drawLine(%pos, %loc, %color, 0.05);
		%line.setNetFlag(6, 1);
	
		%simSet.add(%line);
		%simSet.add(%upperShape);

		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "BALL" TAB %loc TAB %upperShape.getShapeName() TAB %upperShape.getShapeNameColor());
		%simSet.tracerNum++;
		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "LINE" TAB %pos TAB %loc);
		%simSet.tracerNum++;

		%simSet.DisplayTracers();
		return;
	} else {
		%nextPos = vectorAdd(%pos, vectorScale(%vel, 0.032));
		%line = drawLine(%pos, %nextPos, %color, 0.05);
		%line.setNetFlag(6, 1);
	}

	if (isObject(%line)) { 
		%simSet.add(%line);
		setStat("SoccerTracer" @ %simSet.tracerID @ "_" @ %simSet.tracerNum, "LINE" TAB %pos TAB %nextPos);
		%simSet.tracerNum++;
	}
	%simSet.DisplayTracers();

	schedule(32, 0, soccerRaycastTracerLoop, %nextPos, %nextVel, %color, %ignore, %count+1, %simSet);
}

function clearLines() {
	while (isObject(SoccerTracers)) {
		SoccerTracers.deleteAll();
		SoccerTracers.delete();
	}
	while (isObject(ShapeLines)) {
		ShapeLines.delete();
	}
}

function serverCmdClearLines(%cl) {
	if (%cl.isSuperAdmin) {
		clearLines();
		while (isObject(SoccerTracers)) {
			SoccerTracers.delete();
		}
		messageClient(%cl, '', "\c5Lines have been cleared");
	} else {
		messageClient(%cl, '', "You must be a Super Admin to use this command");
	}
}


////////////////////


function StaticShape::showToClient(%shape, %cl) {
	%shape.scopeToClient(%cl);
}

function StaticShape::showToAllClients(%shape) {
	// %shape.setNetFlag(6, 1);
	for (%i = 0; %i < ClientGroup.getCount(); %i++) {
		%shape.scopeToClient(ClientGroup.getCount());
	}
}

function StaticShape::hideFromClient(%shape, %cl) {
	%shape.clearScopeToClient(%cl);
}

function StaticShape::hideFromAllClients(%shape) {
	// %shape.setNetFlag(6, 1);
	for (%i = 0; %i < ClientGroup.getCount(); %i++) {
		%shape.clearScopeToClient(ClientGroup.getCount());
	}
}

function SimSet::displayTracers(%simSet) {
	if (%simSet.getCount() <= %simSet.displayedTracers) {
		return;
	}

	for (%i = %simSet.displayedTracers; %i < %simSet.getCount(); %i++) {
		%obj = %simSet.getObject(%i);
		for (%j = 0; %j < ClientGroup.getCount(); %j++) {
			if ((%cl = ClientGroup.getObject(%j)).isOfficial) {
				%obj.scopeToClient(%cl);
			}
		}
	}

	%simSet.displayedTracers = %simSet.getCount();
}