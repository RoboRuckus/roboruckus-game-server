﻿$(function () {
    $("#shutdown").prop("checked", false);
    var curDamage = parseInt($("#damage").data("damage"));
    // Enable the submit program button
    $("#sendcards img").click(sendCards);
    var submitted = true;
    var isShutdown = false;

    var timer;
    var timeRemaining;
    
    // Set up howler sounds
    var damageSound = new Howl({
        src: ['/sounds/damage.ogg', '/sounds/damage.mp3']
    });
    var laserSound = new Howl({
        src: ['/sounds/laser.ogg', '/sounds/laser.mp3']
    });
    var winnerSound = new Howl({
        src: ['/sounds/winner.ogg', '/sounds/winner.mp3']
    });
    var flagTouch = new Howl({
        src: ['/sounds/flagtouch.ogg', '/sounds/flagtouch.mp3']
    });
    var timerStarted = new Howl({
        src: ['/sounds/timer.ogg', '/sounds/timer.mp3']
    });
    var healSound = new Howl({
        src: ['/sounds/heal.ogg', '/sounds/heal.mp3']
    });
    // Shutdown toggle effects
    $("#shutdown").button().click(function () {
        if ($('#shutdown').prop("checked"))
        {
            $("#labelText").html("Shutdown Next Round &#x2611 On");
        }
        else
        {
            $("#labelText").html("Shutdown Next Round &#x2610 Off");
        }
    });
    $("#shutdownLabel").hover(function () {
        $(this).removeClass("ui-state-hover");
    });

    // Status check button
    $("#statusCheck").button().click(function (event) {
        event.stopImmediatePropagation();
        $('<div id="dialog"></div>').load('/Player/Statuses/ #botStatuses').dialog({
            autoOpen: true,
            height: 'auto',
            width: 'auto',
            title: "Player Stats",
            resizable: "false",
            position: { my: "left top", at: "left top", of: window },
            buttons: [
                {
                    text: "Close",
                    icon: "ui-icon-close",
                    click: function () {
                        $(this).dialog("close");
                    }
                }
            ]
        });
    });

    // Set up arrays for card faces and details
    var faces = new Array();
    faces["right"] = "R";
    faces["left"] = "L";
    faces["uturn"] = "U";
    faces["backup"] = "B";

    var detail = new Array();
    detail["right"] = "Right";
    detail["left"] = "Left";
    detail["backup"] = "Backup";
    detail["uturn"] = "U-Turn";
    detail["forward"] = "Move";

    var boxes = 9;

    // Create connection to player hub
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/playerHub")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    // Processes and displays cards dealt to the player
    connection.on("deal", (cards, lockedCards) => { 
        $('.executing').removeClass('executing');
        submitted = true;
        isShutdown = false;
        $(".slot ul").empty();
        if ($("#submitted").length !== 0) {
            $("#submitted").remove();
        }
        $("#labelText").html("Shutdown Next Round &#x2610 Off");
        $("#shutdown").prop("checked", false);

        var _cards = $.parseJSON(cards);
        var _lockedCards = $.parseJSON(lockedCards);

        if (_cards.length === 0 && _lockedCards.length === 0) {
            $("#shutdownLabel").css("background", "red");
            $("#cardsContainer").html("<h2>" + $("#roboName").data("name") + " is shutdown</h2>");
            isShutdown = true;
            return;
        }
        else
        {
            $("#cardsContainer").empty();
            $("#shutdownLabel").css("background", "none");
            $("#shutdown").button("enable");
        }
        var i = 1;
        $.each(_cards, function () {
            var face;
            var details;
            if (this.direction === "forward") {
                face = this.magnitude;
                details = detail[this.direction] + " " + this.magnitude;
            }
            else {
                face = faces[this.direction];
                details = detail[this.direction];
            }
            // Append card to card container
            $("#cardsContainer").append("<li id='card" + i + "' class='ui-widget-content dealtCard'>\
                <div class='cardBody'>\
                    <p class='order'>" + this.priority + "</p>\
                    <p class='face'>" + face + "</p>\
                    <p class='details'>" + details + "</p>\
                    <img src='/images/cards/bg.png'alt='card'>\
                </div>\
            </li>");
            $("#card" + i).data("cardinfo", this);
            i++;
        });
        // Makes the card slots droppable
        $(".slot").droppable("enable").css("border", "1px solid #DDD");
        // Reset locked card slots
        $(".locked").remove();

        var slot = 5;
        // Process locked cards
        $.each(_lockedCards, function () {
            var face;
            var details;
            if (this.direction === "forward") {
                face = this.magnitude;
                details = detail[this.direction] + " " + this.magnitude;
            }
            else {
                face = faces[this.direction];
                details = detail[this.direction];
            }
            // Add card to locked slot
            $("#slot" + slot + " ul").append("<li style='cursor: default' id='card" + i + "' class='ui-widget-content'>\
                <div class='cardBody'>\
                    <p class='order'>" + this.priority + "</p>\
                    <p class='face'>" + face + "</p>\
                    <p class='details'>" + details + "</p>\
                    <img src='/images/cards/bg.png'alt='card'>\
                </div>\
            </li>");
            $("#slot" + slot).droppable("disable").css("border", "3px solid red");
            $("#slot" + slot + " h4").append("<span class='locked'> LOCKED!</span>");
            $("#card" + i).data("cardinfo", this);
            slot--;
            i++;
        });
        // Minimum width of window is 7 slots worth
        boxes = $(".dealtCard").length < 7 ? 7 : $(".dealtCard").length;

        // Set the width of the cards to fill the screen in one row
        resize();

        // Let the card elements be draggable
        $("li", "#cardsContainer").draggable({
            revert: "invalid", // When not dropped, the item will revert back to its initial position
            containment: "document",
            cursor: "move"
        });
        // Enable the submit program button
        submitted = false;
        $("#submitButton").attr("src", "/images/cards/submit.png");
        // Get bot's current status
        connection.invoke("getHealth", $('#playerNum').data("player")).then(function (damage) {
            updateHealth(damage);
        }).catch (err => console.error(err.toString()));
    });

    // Shows the current register and move being executed 
    connection.on("showMove", (card, robot, register) => { 
        if (curDamage < 10) {
            // Shows register being executed
            $('.executing').removeClass('executing');
            $("#slot" + register + ' .ui-widget-header').addClass('executing');
            // Shows card being executed to all players
            /* Currently disabled
            $("#cardsContainer").empty();
            var _card = $.parseJSON(card);
            var face;
            var details;
            if (_card.direction == "forward") {
                face = _card.magnitude;
                details = detail[_card.direction] + " " + _card.magnitude;
            }
            else {
                face = faces[_card.direction];
                details = detail[_card.direction];
            }
            // Add current card being executed to the card container
            $("#cardsContainer").append("<li class='ui-widget-content dealtCard'>\
                <div class='cardBody'>\
                    <p class='order'>" + _card.priority + "</p>\
                    <p class='face'>" + face + "</p>\
                    <p class='details'>" + details + "</p>\
                    <img src='/images/cards/bg.png'alt='card'>\
                </div>\
            </li>\
            <li id='player'>Robot moving: " + robot + "<\li>"
            );
            resize();
            $("#player").css("font-size", percent * 5.8 + "em");
            */
        }
    });

    // Bind update damage function
    connection.on("UpdateHealth", (damage) => { 
        var curHealth = $.parseJSON(damage);
        var player = parseInt($('#playerNum').data("player"));
        var myDamage = curHealth[player - 1];
        updateHealth(myDamage);
    });

    // When called, requests a deal from the server
    connection.on("requestdeal", (player) => {
        if (player === -1 || player === Number($('#playerNum').data("player"))) {
            $("#shutdown").prop("checked", false).button("refresh");
            connection.invoke("dealMe", $('#playerNum').data("player")).catch(err => console.error(err.toString()));
        }
    });

    // Game has been reset, return to setup page
    connection.on("Reset", (resetAll) => { 
        if (resetAll === 0) {
            setTimeout(function () {
                window.location.assign("/Player/playerSetup/" + $('#playerNum').data("player") + "?reset=1");
            }, 1);
        }
        else
        {
            setTimeout(function () {
                window.location.assign("/");
            }, 1);
        }
    });

    // Displays a message from the server
    connection.on("displayMessage", (message, sound) => { 
        if (curDamage < 10) {
            $("#cardsContainer").html("<h2>" + message + "</h2>");
            switch (sound) {
                case "laser":
                    laserSound.play();
                    break;
                case "winner":
                    winnerSound.play();
                    break;
                case "flagTouch":
                    flagTouch.play();
                    break;
                case "entering":
                    $(".slot ul").empty();
                    break;
            }
        }
    });

    // Starts the countdown timer
    connection.on("startTimer", () => {
        if (!submitted && !isShutdown) {
            timeRemaining = 30;
            $("#timer").html("<h2 style='color: red'>Time remaining: " + timeRemaining + "</h2>");
            timerStarted.play();
            timer = setInterval(timerHandler, 1000);
        }
    });

    // Start the connection to the player hub
    connection.start().then(function () {
        // Once the page loads, get the player's hand if needed
        if ($("#slots").data("started") === "True") {
            connection.invoke("dealMe", $('#playerNum').data("player")).catch(err => console.error(err.toString()));
        }
        else {
            resize();
            $("#cardsContainer").html("<h2 style='color: red;'>Please wait for the game to start.</h2>");
            $("#shutdown").button("disable");
        }
    }).catch(err => console.error(err.toString()));

    // Resize the cards as the window resizes, this is inelegant but works for now, should switch to entirely CSS solution
    $(window).resize(function () {
        resize();
    });

    // Let the slots be droppable, accepting the card elements
    $(".slot").droppable({
        accept: "#cardsContainer > li, .slot li",
        hoverClass: "ui-state-hover",
        drop: function (event, ui) {
            if ($(ui.draggable).parent().hasClass("slotList")) {
                $(ui.draggable).closest("div").droppable("enable");
            }
            $(ui.draggable).css({
                "top": 0,
                "right": 0,
                "left": 0,
                "bottom": 0
            });
            $(ui.draggable).detach().appendTo($("ul", this));
            $(this).droppable("disable");
        }
    });

    // Let the card area be droppable as well, accepting elements from the slots
    $("#cardsContainer").droppable({
        accept: ".slot li",
        activeClass: "custom-state-active",
        drop: function (event, ui) {
            $(ui.draggable).closest("div").droppable("enable");
            $(ui.draggable).css({
                "top": 0,
                "right": 0,
                "left": 0,
                "bottom": 0
            });
            $(ui.draggable).appendTo($(this));
        }
    });

    // Receives the robot's current health from the server
    function updateHealth(damage)
    {
        if (damage > curDamage)
        {
            damageSound.play();
        }
        else if (damage < curDamage)
        {
            healSound.play();
        }
        $(".damageBox").removeClass("filledDamageBox");
        if (damage >= 10) {
            $("#damage").html("Robot is dead! ");
            $("#cardsContainer").html("<h2>Your robot has died!</h2>");
        }
        else {
            $("#damage").html("Robot damage: ");
        }
        for (var i = 0; i <= damage; i++)
        {
            $("#damageBox_" + i).addClass("filledDamageBox");
        }
        curDamage = damage;
        // Gets the flags the robot has touched
        $.get("/Setup/Status", function (data) {
            var player = data.players[parseInt($('#playerNum').data("player")) - 1];
            updateFlags(player);
            updateLives(player);
            updateBot(player);
        });
    }

    // Draws the flags and touched flags
    function updateFlags(player) {
        if (player.totalFlags > 0) {
            $("#flagBoxes").empty();
            for (var i = 0; i < player.totalFlags; i++) {
                $("#flagBoxes").append('<span id="flag' + i + '" class="flagBox"></span> ');
                if (player.flags > i) {
                    $("#flag" + i).addClass("touchedFlag");
                }
            }
        } else {
            $(".flagInfo").remove();
        }
    }

    // Updates the life boxes
    function updateLives(player) {
        $(".lifeBox").addClass("emptyLifeBox");
        for (var i = 1; i <= player.lives; i++) {
            $("#lifeBox_" + i).removeClass("emptyLifeBox");
        }
        if (player.lives <=0)
        {
            $("#cardsContainer").html("<h2>You lose :-(</h2>");
        }
    }

    // Updates the bot assigned to the player
    function updateBot(player) {
        $("#roboName").html("Robot: " + player.name);
    }

    // Sends the selected cards to the server
    function sendCards() {
        if (!submitted) {
            submitted = true;
            if ($(".slot").has("li").length !== $(".slot").length) {
                alert("Please fill all movement slots");
                submitted = false;
            }
            else {
                window.clearInterval(timer);
                $("#timer").empty();
                $("#submitButton").attr("src", "/images/cards/submitted.png");
                $("#shutdown").button("disable");
                var shutdown = false;
                var move = new Array();
                $(".slot").each(function () {
                    move.push($("li", this).data("cardinfo"));
                });
                $(".ui-draggable").draggable("option", "disabled", true).css("cursor", "default");
                if ($('#shutdown').prop("checked")) {
                    shutdown = true;
                }
                connection.invoke("sendCards", $('#playerNum').data("player"), move, shutdown).catch(err => console.error(err.toString()));
                $("#slots").prepend("<h3 id='submitted' style='color: red'>Program Submitted</h3>");
            }
        }
    }
    
    // Handles the countdown timer
    function timerHandler()
    {
        // Count down
        timeRemaining--;
        $("#timer").html("<h2 style='color: red'>Time remaining: " + timeRemaining + "</h2>");
        if (timeRemaining === 0)
        {
            clearInterval(timer);
            // Deal random cards into the register slots
            $(".slot").each(function () {
                if (!$(this).has("li").length)
                {
                    var number = Math.floor((Math.random() * $('#cardsContainer li').length) + 1);
                    $('#cardsContainer li:nth-child(' + number + ')').detach().appendTo($(this).find('.slotList'));
                }
            });
            $(".ui-draggable").draggable("option", "disabled", true).css("cursor", "default");
            setTimeout(sendCards, 1500);
        }
    }

    function resize()
    {
        var imageWidth = ($(window).width() - 80) / boxes;
        if (imageWidth < 350) {
            var percent = imageWidth / 350;
            var imageHeight = percent * 520;
            $(".order").css("font-size", percent * 4 + "em");
            $(".face").css("font-size", percent * 11.8 + "em");
            $(".details").css("font-size", percent * 2.5 + "em");
            $("#cardsContainer img, .slot img").width(imageWidth);
            $("#cardsContainer img, .slot img").height(imageHeight);
            $("#cardsContainer li, .slot li").css({
                "height": "",
                "width": ""
            });
            $(".slot, #sendcards").width(imageWidth + 2);
            $(".slot, #sendcards").height(imageHeight + 28);
            $("#cardsContainer").css("min-height", imageHeight + 5);
        }
        else
        {
            $(".order").css("font-size", "2.15em");
            $("#cardsContainer img, .slot img").width(350);
            $("#cardsContainer img, .slot img").height(520);
            $("#cardsContainer li, .slot li").css({
                "height": "",
                "width": ""
            });
            $(".slot, #sendcards").width(352);
            $(".slot, #sendcards").height(548);
            $("#cardsContainer").css("min-height", 525);
        }
        $("#labelText").css("font-size", 1.35 + (0.3 * (9 - boxes)) + "vw");
    }
});