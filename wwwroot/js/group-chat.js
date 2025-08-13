// Group Chat Modern UI Logic
(function() {
    // ==== STATE ====
    const groupId = window.__groupId || 0;
    const groupName = window.__groupName || '';
    const currentUser = window.__currentUser || '';
    const currentUserDisplayName = window.__currentUserDisplayName || '';
    const currentUserProfileImage = window.__currentUserProfileImage || '';
    const isGroupAdmin = window.__isGroupAdmin || false;
    
    let connection = null;
    let isConnecting = false;
    let groupMembers = []; // Will be loaded from server

    // ==== DOM ELEMENTS ====
    const groupMessages = document.getElementById('groupMessages');
    const groupMessageInput = document.getElementById('groupMessageInput');
    const groupSendButton = document.getElementById('groupSendButton');
    const groupCharCount = document.getElementById('groupCharCount');
    const groupHint = document.getElementById('groupHint');
    const btnGroupImageUpload = document.getElementById('btnGroupImageUpload');
    const groupImageInput = document.getElementById('groupImageInput');
    const membersList = document.getElementById('membersList');
    const memberSearch = document.getElementById('memberSearch');
    const memberCount = document.getElementById('memberCount');

    if (!groupId || !groupMessages) {
        console.error('Group chat: Missing required elements or group ID');
        return;
    }

    // ==== SIGNALR CONNECTION ====
    function ensureConnection() {
        if (connection || isConnecting) return;
        
        isConnecting = true;
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/chathub')
            .withAutomaticReconnect()
            .build();

        // Group message received
        connection.on('ReceiveGroupMessage', (message) => {
            if (message.GroupId === groupId) {
                addGroupMessage(message.FromUser, message.Message, message.Timestamp, message.MessageType);
            }
        });

        // User joined group
        connection.on('UserJoinedGroup', (groupId, username, displayName) => {
            if (groupId === window.__groupId) {
                addSystemMessage(`${displayName} gruba katıldı`);
                updateMemberCount();
            }
        });

        // User left group
        connection.on('UserLeftGroup', (groupId, username, displayName) => {
            if (groupId === window.__groupId) {
                addSystemMessage(`${displayName} gruptan ayrıldı`);
                updateMemberCount();
            }
        });

        connection.start()
            .then(() => {
                isConnecting = false;
                groupHint.textContent = 'Gruba mesaj gönderebilirsiniz';
                console.log('Group chat connected');
                
                // Join group room
                return connection.invoke('JoinGroup', groupId);
            })
            .then(() => {
                loadGroupMessages();
                loadGroupMembers();
            })
            .catch(err => {
                console.error('Group chat connection error:', err);
                groupHint.textContent = 'Bağlantı hatası';
                isConnecting = false;
                setTimeout(ensureConnection, 2000);
            });
    }

    // ==== MESSAGE FUNCTIONS ====
    function addGroupMessage(fromUser, message, timestamp, messageType = 'TEXT') {
        const isOwn = fromUser.Username === currentUser;
        const group = document.createElement('div');
        group.className = 'message-group group-message ' + (isOwn ? 'mine' : '');

        // Avatar
        const avatarImg = fromUser.ProfileImageUrl ? 
            `<img src="${fromUser.ProfileImageUrl}" alt="${fromUser.DisplayName}">` : '';
        const avatar = `<div class="avatar">${avatarImg}<span style="display:${fromUser.ProfileImageUrl ? 'none' : 'flex'}">${(fromUser.DisplayName || fromUser.Username).charAt(0).toUpperCase()}</span></div>`;

        // Time
        const timeStr = timestamp ? 
            new Date(timestamp).toLocaleTimeString('tr-TR', {hour: '2-digit', minute: '2-digit'}) :
            new Date().toLocaleTimeString('tr-TR', {hour: '2-digit', minute: '2-digit'});

        // Sender name (only for others' messages)
        const senderName = !isOwn ? `<div class="sender-name">${fromUser.DisplayName || fromUser.Username}</div>` : '';

        // Message content
        let messageContent;
        if (messageType === 'IMAGE' || (message.startsWith('[RESIM:') && message.endsWith(']'))) {
            const imageUrl = messageType === 'IMAGE' ? message : message.slice(7, -1);
            messageContent = `<img src="${imageUrl}" alt="Paylaşılan resim" style="max-width: 300px; max-height: 300px; border-radius: 8px; cursor: pointer;" onclick="window.open('${imageUrl}', '_blank')">`;
        } else {
            messageContent = `<span class="message-text">${escapeHtml(message)}</span>`;
        }

        // Edit button (only for own messages)
        const editBtn = isOwn ? `<button class="btn-edit" onclick="editGroupMessage(this)" title="Düzenle">⋯</button>` : '';

        const bubbleClass = isOwn ? 'bubble bubble--me' : 'bubble';
        
        group.innerHTML = avatar + `
            <div class="bubble-stack">
                ${senderName}
                <div class="${bubbleClass}" data-message-id="${Date.now()}">
                    ${messageContent}
                    ${editBtn}
                    <small class="meta">${timeStr}</small>
                </div>
            </div>`;

        groupMessages.appendChild(group);
        groupMessages.scrollTop = groupMessages.scrollHeight;
    }

    function addSystemMessage(message) {
        const div = document.createElement('div');
        div.className = 'system-msg text-center text-dim small';
        div.innerHTML = `<i class="bi bi-info-circle me-1"></i>${message}`;
        groupMessages.appendChild(div);
        groupMessages.scrollTop = groupMessages.scrollHeight;
    }

    function sendGroupMessage() {
        const text = groupMessageInput.value.trim();
        if (!text || !connection) return;

        if (connection.state !== signalR.HubConnectionState.Connected) {
            groupHint.textContent = 'Bağlı değil';
            return;
        }

        connection.invoke('SendGroupMessage', groupId, currentUser, text)
            .then(() => {
                groupMessageInput.value = '';
                groupCharCount.textContent = '0';
                autoGrow(groupMessageInput);
            })
            .catch(err => {
                console.error('Send group message error:', err);
                groupHint.textContent = 'Mesaj gönderilemedi';
            });
    }

    // ==== LOAD FUNCTIONS ====
    function loadGroupMessages() {
        fetch(`/Chat/GetGroupMessages?groupId=${groupId}`)
            .then(response => response.json())
            .then(data => {
                if (data.success && data.messages) {
                    // Clear existing messages except welcome
                    const messages = groupMessages.querySelectorAll('.message-group, .system-msg:not(#groupWelcome)');
                    messages.forEach(msg => msg.remove());

                    // Add messages
                    data.messages.forEach(msg => {
                        addGroupMessage(msg.FromUser, msg.Message, msg.Timestamp, msg.MessageType);
                    });
                }
            })
            .catch(err => console.error('Error loading group messages:', err));
    }

    function loadGroupMembers() {
        fetch(`/Chat/GetGroupMembers?groupId=${groupId}`)
            .then(response => response.json())
            .then(data => {
                if (data.success && data.members) {
                    groupMembers = data.members;
                    updateMemberCount();
                    renderMembersList();
                }
            })
            .catch(err => console.error('Error loading group members:', err));
    }

    function updateMemberCount() {
        if (memberCount) {
            const count = groupMembers.filter(m => m.IsActive).length;
            memberCount.textContent = `${count} üye`;
        }
    }

    function renderMembersList() {
        if (!membersList) return;

        const searchTerm = memberSearch ? memberSearch.value.toLowerCase() : '';
        const filteredMembers = groupMembers.filter(member => 
            member.IsActive && 
            (member.User.DisplayName.toLowerCase().includes(searchTerm) || 
             member.User.Username.toLowerCase().includes(searchTerm))
        );

        membersList.innerHTML = filteredMembers.map(member => `
            <div class="member-item d-flex align-items-center mb-2 p-2 rounded" data-member-id="${member.UserId}">
                <div class="avatar me-3">
                    ${member.User.ProfileImageUrl ? 
                        `<img src="${member.User.ProfileImageUrl}" alt="${member.User.DisplayName}">` :
                        `<span>${member.User.DisplayName.charAt(0).toUpperCase()}</span>`
                    }
                </div>
                <div class="flex-grow-1">
                    <div class="fw-medium">${member.User.DisplayName}</div>
                    <small class="text-muted">@${member.User.Username}</small>
                    ${member.IsAdmin ? '<span class="badge bg-primary ms-2">Admin</span>' : ''}
                </div>
                <div class="member-actions">
                    ${renderMemberActions(member)}
                </div>
            </div>
        `).join('');
    }

    function renderMemberActions(member) {
        if (!isGroupAdmin || member.User.Username === currentUser) return '';

        return `
            <button class="btn btn-sm btn-outline-danger" onclick="removeMember(${member.UserId})" title="Gruptan Çıkar">
                <i class="bi bi-person-x"></i>
            </button>
        `;
    }

    // ==== UTILITY FUNCTIONS ====
    function escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, m => map[m]);
    }

    function autoGrow(element) {
        element.style.height = 'auto';
        element.style.height = Math.min(element.scrollHeight, 120) + 'px';
    }

    // ==== EVENT LISTENERS ====
    if (groupSendButton) {
        groupSendButton.addEventListener('click', sendGroupMessage);
    }

    if (groupMessageInput) {
        groupMessageInput.addEventListener('input', () => {
            groupCharCount.textContent = groupMessageInput.value.length;
            autoGrow(groupMessageInput);
        });

        groupMessageInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                if (groupMessageInput.value.trim()) {
                    sendGroupMessage();
                }
            }
        });
    }

    // Image upload
    if (btnGroupImageUpload && groupImageInput) {
        btnGroupImageUpload.addEventListener('click', () => {
            groupImageInput.click();
        });

        groupImageInput.addEventListener('change', async (e) => {
            const file = e.target.files[0];
            if (!file) return;

            // Validate file
            if (!file.type.startsWith('image/')) {
                alert('Lütfen sadece resim dosyaları seçin.');
                return;
            }

            if (file.size > 5 * 1024 * 1024) {
                alert('Dosya boyutu çok büyük. Maksimum 5MB olabilir.');
                return;
            }

            try {
                const formData = new FormData();
                formData.append('image', file);
                formData.append('groupId', groupId);

                btnGroupImageUpload.disabled = true;
                btnGroupImageUpload.innerHTML = '<i class="bi bi-hourglass-split"></i>';

                const response = await fetch('/Chat/UploadGroupImage', {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();

                if (result.success) {
                    // Send image message via SignalR
                    await connection.invoke('SendGroupMessage', groupId, currentUser, result.imageUrl, 'IMAGE');
                } else {
                    alert('Resim yüklenirken hata oluştu: ' + result.message);
                }
            } catch (error) {
                console.error('Image upload error:', error);
                alert('Resim yüklenirken hata oluştu.');
            } finally {
                btnGroupImageUpload.disabled = false;
                btnGroupImageUpload.innerHTML = '<i class="bi bi-image"></i>';
                groupImageInput.value = '';
            }
        });
    }

    // Member search
    if (memberSearch) {
        memberSearch.addEventListener('input', renderMembersList);
    }

    // ==== GLOBAL FUNCTIONS FOR INLINE EVENTS ====
    window.editGroupMessage = function(editBtn) {
        const bubble = editBtn.closest('.bubble');
        const messageTextSpan = bubble.querySelector('.message-text');
        const originalText = messageTextSpan.textContent;

        // Create edit interface (similar to previous edit functions)
        const editInput = document.createElement('input');
        editInput.type = 'text';
        editInput.value = originalText;
        editInput.className = 'edit-input';
        editInput.style.cssText = 'width: 100%; border: 1px solid #ddd; border-radius: 8px; padding: 8px; margin: 4px 0; font-size: 14px;';

        const buttonContainer = document.createElement('div');
        buttonContainer.style.cssText = 'display: flex; gap: 5px; margin-top: 5px;';

        const saveBtn = document.createElement('button');
        saveBtn.textContent = 'Kaydet';
        saveBtn.className = 'btn btn-sm btn-primary';
        saveBtn.style.cssText = 'font-size: 11px; padding: 2px 8px;';

        const cancelBtn = document.createElement('button');
        cancelBtn.textContent = 'İptal';
        cancelBtn.className = 'btn btn-sm btn-secondary';
        cancelBtn.style.cssText = 'font-size: 11px; padding: 2px 8px;';

        buttonContainer.appendChild(saveBtn);
        buttonContainer.appendChild(cancelBtn);

        const originalContent = messageTextSpan.innerHTML;
        messageTextSpan.innerHTML = '';
        messageTextSpan.appendChild(editInput);
        messageTextSpan.appendChild(buttonContainer);

        editBtn.style.display = 'none';
        editInput.focus();
        editInput.select();

        function saveEdit() {
            const newText = editInput.value.trim();
            if (newText && newText !== originalText) {
                let editIndicator = bubble.querySelector('.edit-indicator');
                if (!editIndicator) {
                    editIndicator = document.createElement('small');
                    editIndicator.className = 'edit-indicator text-muted';
                    editIndicator.style.cssText = 'font-size: 10px; margin-left: 8px; opacity: 0.7;';
                    editIndicator.textContent = '(düzenlendi)';
                }
                
                messageTextSpan.innerHTML = escapeHtml(newText);
                messageTextSpan.appendChild(editIndicator);
            } else {
                messageTextSpan.innerHTML = originalContent;
            }
            editBtn.style.display = 'inline-block';
        }

        function cancelEdit() {
            messageTextSpan.innerHTML = originalContent;
            editBtn.style.display = 'inline-block';
        }

        saveBtn.addEventListener('click', saveEdit);
        cancelBtn.addEventListener('click', cancelEdit);

        editInput.addEventListener('keydown', function(e) {
            if (e.key === 'Enter') {
                e.preventDefault();
                saveEdit();
            } else if (e.key === 'Escape') {
                e.preventDefault();
                cancelEdit();
            }
        });
    };

    window.removeMember = function(userId) {
        if (!confirm('Bu üyeyi gruptan çıkarmak istediğinizden emin misiniz?')) return;

        fetch('/Chat/RemoveGroupMember', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ groupId: groupId, userId: userId })
        })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                loadGroupMembers();
                addSystemMessage('Bir üye gruptan çıkarıldı');
            } else {
                alert('Üye çıkarılırken hata oluştu: ' + data.message);
            }
        })
        .catch(error => {
            console.error('Error removing member:', error);
            alert('Üye çıkarılırken hata oluştu.');
        });
    };

    // ==== INITIALIZATION ====
    ensureConnection();

})();
