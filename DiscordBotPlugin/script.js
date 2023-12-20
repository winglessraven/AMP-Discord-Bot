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

function fetchDataAndUpdateUI() {
    fetch('panel.json')
        .then(response => response.json())
        .then(data => {
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

    // Update Online Players
    const onlinePlayersContainer = document.getElementById('online-players-container');
    if (data.OnlinePlayers && data.OnlinePlayers.length > 0) {
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

// Function to increment playtime for online players
function incrementPlaytimeForOnlinePlayers() {
    const onlinePlayers = document.querySelectorAll('#online-players .flex-item p');
    onlinePlayers.forEach(playerElement => {
        const playerName = playerElement.textContent.trim();
        const playtimeElements = document.querySelectorAll('#playtime-leaderboard li');
        playtimeElements.forEach(playtimeElement => {
            if (playtimeElement.textContent.includes(playerName)) {
                const parts = playtimeElement.textContent.split(' - ');
                const incrementedPlaytime = incrementTime(parts[1]);
                playtimeElement.textContent = `${playerName} - ${incrementedPlaytime}`;
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
