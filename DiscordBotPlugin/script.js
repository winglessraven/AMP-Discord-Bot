function copyToClipboard(elementId) {
    var textElement = document.getElementById(elementId);
    var text = textElement.innerText;
    navigator.clipboard.writeText(text).then(function () {
        // Change text to "Copied to Clipboard"
        textElement.textContent = 'Copied to Clipboard';
        setTimeout(function () {
            // Revert back to the original text
            textElement.textContent = text;
        }, 2000); // after 2 seconds
    }, function (err) {
        console.error('Could not copy text: ', err);
    });
}

function incrementTime(timeString) {
    let [days, hours, minutes, seconds] = timeString.split(':').map(Number);
    seconds += 1;
    if (seconds >= 60) {
        seconds = 0;
        minutes += 1;
        if (minutes >= 60) {
            minutes = 0;
            hours += 1;
            if (hours >= 24) {
                hours = 0;
                days += 1;
            }
        }
    }
    return [
        days.toString().padStart(2, '0'),
        hours.toString().padStart(2, '0'),
        minutes.toString().padStart(2, '0'),
        seconds.toString().padStart(2, '0')
    ].join(':');
}

function updateUptime() {
    const statusElement = document.querySelector('.status p'); // Get the current status
    const uptimeElement = document.getElementById('uptime');

    // Check if the status text is "Ready" before incrementing the uptime
    if (statusElement && statusElement.textContent.includes('Ready')) {
        uptimeElement.textContent = incrementTime(uptimeElement.textContent);
    }
}

setInterval(updateUptime, 1000); // Call updateUptime every second

let currentData = {}; // Variable to store the latest fetched data

function fetchDataAndUpdateUI() {
    fetch('panel.json')
        .then(response => response.json())
        .then(data => {
            currentData = data; // Store the latest data
            updateUI(data);
        })
        .catch(error => console.error('Error fetching data:', error));
}

function updateUI(data) {
    // Update elements based on JSON data
    document.getElementById('server-name').textContent = data.ServerName;
    document.getElementById('server-ip').textContent = data.ServerIP;
    document.querySelector('.status p').className = data.ServerStatusClass;
    document.querySelector('.status p').textContent = data.ServerStatus;
    document.getElementById('cpu-usage').textContent = data.CPUUsage;
    document.getElementById('memory-usage').textContent = data.MemoryUsage;
    document.getElementById('uptime').textContent = data.Uptime;

    console.log("Updating Online Players in UI");
    const onlinePlayersContainer = document.getElementById('online-players-container');
    if (data.OnlinePlayers && data.OnlinePlayers.length > 0) {
        console.log(`Online players from JSON: ${data.OnlinePlayers.join(', ')}`);
        onlinePlayersContainer.style.display = 'block';
        const onlinePlayersList = onlinePlayersContainer.querySelector('.flex-item');
        onlinePlayersList.innerHTML = '<h2>Online Players</h2>' + data.OnlinePlayers.map(player => `<p>${player}</p>`).join('');
    } else {
        onlinePlayersContainer.style.display = 'none';
    }

    // Update Player Count
    const playerCountContainer = document.getElementById('player-count-container');
    if (data.PlayerCount) {
        playerCountContainer.style.display = 'block';
        document.getElementById('player-count').textContent = data.PlayerCount;
    } else {
        playerCountContainer.style.display = 'none';
    }

    // Update Playtime Leaderboard
    if (data.PlaytimeLeaderBoard && data.PlaytimeLeaderBoard.length > 0) {
        const playtimeLeaderboardList = document.getElementById('playtime-leaderboard');
        playtimeLeaderboardList.innerHTML = data.PlaytimeLeaderBoard.map(entry => `<li>${entry}</li>`).join('');
    }
}

// Function to increment player playtime in 'Xd Xh Xm Xs' format
function incrementPlayerPlaytime(playtimeString) {
    let [days, hours, minutes, seconds] = playtimeString.split(/d |h |m |s/).map(Number);
    seconds++;
    if (seconds >= 60) {
        seconds = 0;
        minutes++;
        if (minutes >= 60) {
            minutes = 0;
            hours++;
            if (hours >= 24) {
                hours = 0;
                days++;
            }
        }
    }
    return `${days}d ${hours}h ${minutes}m ${seconds}s`;
}

// Function to increment playtime for online players
function incrementPlaytimeForOnlinePlayers() {
    //console.log("incrementPlaytimeForOnlinePlayers function called");

    if (!currentData.OnlinePlayers || currentData.OnlinePlayers.length === 0) {
        //console.log("No online players found");
        return;
    }

    //console.log(`Found ${currentData.OnlinePlayers.length} online players from JSON`);

    currentData.OnlinePlayers.forEach(playerName => {
        //console.log(`Processing player: ${playerName}`);
        const playtimeElements = document.querySelectorAll('#playtime-leaderboard li');
        //console.log(`Found ${playtimeElements.length} playtime elements`);

        playtimeElements.forEach(playtimeElement => {
            //console.log(`Checking playtime element: ${playtimeElement.textContent}`);

            if (playtimeElement.textContent.includes(playerName)) {
                //console.log(`Before Increment - Player: ${playerName}, Playtime: ${playtimeElement.textContent}`);
                const parts = playtimeElement.textContent.split(' - ');
                const incrementedPlaytime = incrementPlayerPlaytime(parts[1].trim());
                playtimeElement.textContent = `${playerName} - ${incrementedPlaytime}`;
                //console.log(`After Increment - Player: ${playerName}, Playtime: ${playtimeElement.textContent}`);
            }
        });
    });
}

// Call this function periodically (e.g., every second)
setInterval(incrementPlaytimeForOnlinePlayers, 1000);

// Initial call to fetch and update data
fetchDataAndUpdateUI();

// Set up interval to update data every 10 seconds
setInterval(fetchDataAndUpdateUI, 10000);
