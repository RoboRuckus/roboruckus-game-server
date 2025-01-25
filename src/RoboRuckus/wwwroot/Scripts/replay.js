let selectedGame = 0;
$(function () {
    $.get({
        url: "/Setup/startReplay?gameID=0&logger=0",
        dataType: "json",
        success: function (data) { processLogData(data); }
    });
});

function processLogData(data) {
    for (const [id, date] of Object.entries(data)) {
        const offset = new Date().getTimezoneOffset();
        const dateString = new Date(date);
        dateString.setMinutes(dateString.getMinutes() - offset);
        $("#loggedGames").append('<p class="loggedGame" data-game_id="' + id + '">' + dateString.toString().substring(0, dateString.toString().indexOf("GMT")) + '</p>');
    }

    $(".loggedGame").click(function() {
        selectedGame = $(this).data("game_id");
        $.get({
            url: "/Setup/startReplay?gameID=" + $(this).data("game_id") + "&logger=0&startRound=0",
            dataType: "json",
            success: function (data) { processEvent(data); }
        });
    });
}

function processEvent(data) {
    $("#gameEvents").empty();
    $("#gameEvents").append("<p>Game rounds</p>");
    let i = 1;
    data.forEach(function (item) {
        $("#gameEvents").append('<p class="gameEvent"><a href="/Setup/startReplay?logger=0&gameID=' + selectedGame + '&startRound=' + item + '">Round ' + i + '</a></p>');
        i++;
    });
}