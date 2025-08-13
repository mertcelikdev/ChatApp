// Private chat modern UI logic (extracted from Razor to avoid parsing issues)
(function(){
  // ==== STATE ====
  const currentUser = window.__currentUser || '';
  let selectedUser = null; // username string
  let connection = null; let isConnecting=false;
  let recentConversations = []; // {user, unread, time, displayName, profileImageUrl, isOnline}
  let userDirectory = []; // fetched on-demand for modal

  // ==== DOM ====
  const listEl = document.getElementById('conversationList'); if(!listEl) return;
  const convEmpty = document.getElementById('convEmpty');
  const headerName = document.getElementById('headerName');
  const headerStatus = document.getElementById('headerStatus');
  const headerAvatar = document.getElementById('headerAvatar');
  const headerSub = document.getElementById('headerSub');
  const messageInput = document.getElementById('messageInput');
  const sendButton = document.getElementById('sendButton');
  const messagesArea = document.getElementById('messagesArea');
  const composerHint = document.getElementById('composerHint');
  const charCount = document.getElementById('charCount');
  const toggleDetails = document.getElementById('toggleDetails');
  const btnNewChat = document.getElementById('btnNewChat');
  const welcomeState = document.getElementById('welcomeState');
  const chatInterface = document.getElementById('chatInterface');
  const startNewChatBtn = document.getElementById('startNewChatBtn');
  const closeChatBtn = document.getElementById('closeChatBtn');

  // Modal elements (created lazily once)
  let modalEl = null; let modalInstance = null; let modalList = null; let modalSearchInput=null; let modalSpinner=null;

  function ensureConnection(){
    if(connection || isConnecting) return; isConnecting=true;
    connection = new signalR.HubConnectionBuilder().withUrl('/chathub').withAutomaticReconnect().build();
    connection.on('ReceiveMessage', p=>{ if(p && p.MessageType==='Private'){ handleIncoming(p.From, p.Message, p.Timestamp, p.To);} });
    connection.start().then(()=>{ isConnecting=false; headerSub.textContent='Bağlandı'; refreshPresence(); }).catch(err=>{ console.error(err); headerSub.textContent='Bağlantı hatası'; isConnecting=false; setTimeout(ensureConnection,2000); });
  }
  ensureConnection();

  // Presence refresh from backend (users in conversations only)
  function refreshPresence(){
    if(recentConversations.length===0) return;
    const qs = recentConversations.map(c=>'u='+encodeURIComponent(c.user)).join('&');
    fetch('/Chat/GetUsers') // simple: fetch all and map
      .then(r=>r.json())
      .then(d=>{ if(d.success){ const map={}; d.users.forEach(u=> map[u.username]=u); recentConversations.forEach(c=>{ if(map[c.user]){ c.isOnline=map[c.user].isOnline; c.profileImageUrl=map[c.user].profileImageUrl; }}); renderConversationList(); if(selectedUser){ updateHeaderUser(selectedUser); } } })
      .catch(()=>{});
  }
  setInterval(refreshPresence, 15000);

  function renderConversationList(){
    listEl.innerHTML='';
    if(recentConversations.length===0){ convEmpty.classList.remove('d-none'); return;} else convEmpty.classList.add('d-none');
    recentConversations.forEach(c=>{
      const div=document.createElement('div');
      const active = selectedUser===c.user? ' active':'';
      const unread = c.unread? ' unread':'';
      const presenceCls = c.isOnline? 'presence-online': 'presence-offline';
      const avatarInner = c.profileImageUrl? `<img src="${c.profileImageUrl}" alt="${c.displayName||c.user}">` : (c.displayName||c.user||'?').charAt(0).toUpperCase();
      div.className='chat-item'+active+unread; div.dataset.user=c.user;
      div.innerHTML=`<div class="avatar">${avatarInner}<span class="presence-dot ${presenceCls}"></span></div>
        <div class="flex-grow-1">
          <div class="d-flex align-items-center gap-1"><span class="chat-title">${c.displayName||c.user}</span></div>
          <div class="small text-dim text-truncate" style="max-width:160px;">&nbsp;</div>
        </div>
        <div class="text-end" style="min-width:46px;"><time class="d-block text-dim" style="font-size:11px;">${c.time||''}</time>${c.unread?'<span class="unread-badge">'+c.unread+'</span>':''}</div>`;
      div.onclick=()=> selectConversation(c.user);
      listEl.appendChild(div);
    });
  }

  function upsertConversation(user, meta){
    let c = recentConversations.find(x=>x.user===user);
    if(!c){ c={user, displayName:meta?.displayName||user, profileImageUrl:meta?.profileImageUrl||null, isOnline:meta?.isOnline||false, unread:0, time:new Date().toLocaleTimeString('tr-TR',{hour:'2-digit','minute':'2-digit'})}; recentConversations.unshift(c);} else { if(meta){ c.displayName=meta.displayName||c.displayName; c.profileImageUrl=meta.profileImageUrl||c.profileImageUrl; c.isOnline= meta.isOnline; }}
    renderConversationList();
  }

  function selectConversation(user){ 
    selectedUser=user; 
    updateHeaderUser(user); 
    messageInput.disabled=false; 
    sendButton.disabled=false; 
    composerHint.textContent='Enter gönder, Shift+Enter yeni satır'; 
    recentConversations.forEach(c=>{ if(c.user===user) c.unread=0; }); 
    renderConversationList(); 
    const sys=document.getElementById('systemInit'); if(sys) sys.remove(); 
    // Show chat interface and hide welcome state
    if(welcomeState) welcomeState.classList.add('d-none');
    if(chatInterface) chatInterface.classList.remove('d-none');
  }

  function updateHeaderUser(user){
    const c = recentConversations.find(x=>x.user===user);
    if(!c){ headerName.textContent=user; headerStatus.textContent=''; return; }
    headerName.textContent=c.displayName||c.user;
    headerStatus.textContent= c.isOnline? 'Online':'Offline';
    headerStatus.classList.toggle('status-offline', !c.isOnline);
    if(c.profileImageUrl){ headerAvatar.innerHTML='<img src="'+c.profileImageUrl+'" alt="'+(c.displayName||c.user)+'" style="width:100%;height:100%;object-fit:cover;border-radius:50%;">'; }
    else { headerAvatar.textContent=(c.displayName||c.user).charAt(0).toUpperCase(); }
    // click to enlarge avatar
    headerAvatar.onclick=()=>{ if(!c.profileImageUrl) return; showImageLightbox(c.profileImageUrl, c.displayName||c.user); };
  }

  function handleIncoming(from, message, timestamp, to){
    const mine = from===currentUser; const otherUser = mine? to: from;
    upsertConversation(otherUser); if(!mine && otherUser!==selectedUser){ const conv=recentConversations.find(c=>c.user===otherUser); if(conv){ conv.unread=(conv.unread||0)+1; renderConversationList(); }}
    addBubble(mine?'mine':'other', from, message, timestamp);
  }

  function addBubble(type, from, text, ts){
    const group=document.createElement('div'); group.className='message-group '+(type==='mine'?'mine':'');
    
    // Get avatar content based on message type
    let avatarContent;
    if (type === 'mine') {
      // For user's own messages, use current user's profile image if available
      avatarContent = window.__currentUserProfileImage ? 
        `<img src="${window.__currentUserProfileImage}" alt="${window.__currentUserDisplayName || currentUser}">` : 
        (window.__currentUserDisplayName || currentUser || '?').charAt(0).toUpperCase();
    } else {
      // For other user's messages, use their profile image from conversation data
      const conv = recentConversations.find(c=>c.user===from) || recentConversations.find(c=>c.user===selectedUser);
      avatarContent = conv && conv.profileImageUrl ? `<img src="${conv.profileImageUrl}" alt="${conv.displayName||from}">` : ((from||'?').charAt(0).toUpperCase());
    }
    
    group.innerHTML='<div class="avatar">'+avatarContent+'</div><div class="bubble-stack"><div class="bubble '+(type==='mine'?'bubble--me':'bubble--other')+'" data-message-id="'+Date.now()+'"><span class="message-text">'+escapeHtml(text)+'</span>'+(type==='mine'?'<button class="btn-edit" onclick="editPrivateMessage(this)" title="Düzenle">⋯</button>':'')+'<small class="meta">'+formatTime(ts)+'</small></div></div>';
    messagesArea.appendChild(group); messagesArea.scrollTo({top:messagesArea.scrollHeight,behavior:'smooth'});
  }

  function formatTime(ts){ const d= ts? new Date(ts): new Date(); return d.toLocaleTimeString('tr-TR',{hour:'2-digit',minute:'2-digit'}); }

  // Edit private message functionality
  function editPrivateMessage(editBtn) {
    const bubble = editBtn.closest('.bubble');
    const messageTextSpan = bubble.querySelector('.message-text');
    const originalText = messageTextSpan.textContent;
    
    // Create edit input
    const editInput = document.createElement('input');
    editInput.type = 'text';
    editInput.value = originalText;
    editInput.className = 'edit-input';
    editInput.style.cssText = 'width: 100%; border: 1px solid #ddd; border-radius: 8px; padding: 8px; margin: 4px 0; font-size: 14px;';
    
    // Create save/cancel buttons
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
    
    // Replace message content with edit interface
    const originalContent = messageTextSpan.innerHTML;
    messageTextSpan.innerHTML = '';
    messageTextSpan.appendChild(editInput);
    messageTextSpan.appendChild(buttonContainer);
    
    // Hide edit button during editing
    editBtn.style.display = 'none';
    
    // Focus on input
    editInput.focus();
    editInput.select();
    
    // Save functionality
    function saveEdit() {
        const newText = editInput.value.trim();
        if (newText && newText !== originalText) {
            // Check if bubble already has edit indicator
            let editIndicator = bubble.querySelector('.edit-indicator');
            if (!editIndicator) {
                editIndicator = document.createElement('small');
                editIndicator.className = 'edit-indicator text-muted';
                editIndicator.style.cssText = 'font-size: 10px; margin-left: 8px; opacity: 0.7;';
                editIndicator.textContent = '(düzenlendi)';
            }
            
            messageTextSpan.innerHTML = escapeHtml(newText);
            messageTextSpan.appendChild(editIndicator);
            // Here you would typically send the edit to the server
            // connection.invoke("EditPrivateMessage", messageId, newText);
        } else {
            messageTextSpan.innerHTML = originalContent;
        }
        editBtn.style.display = 'inline-block';
    }
    
    // Cancel functionality
    function cancelEdit() {
        messageTextSpan.innerHTML = originalContent;
        editBtn.style.display = 'inline-block';
    }
    
    // Event listeners
    saveBtn.addEventListener('click', saveEdit);
    cancelBtn.addEventListener('click', cancelEdit);
    
    // Enter to save, Escape to cancel
    editInput.addEventListener('keydown', function(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            saveEdit();
        } else if (e.key === 'Escape') {
            e.preventDefault();
            cancelEdit();
        }
    });
  }

  // Make editPrivateMessage available globally
  window.editPrivateMessage = editPrivateMessage;
  function escapeHtml(str){ return str.replace(/[&<>"']/g,m=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[m])); }

  // Edit message functionality for private chat
  window.editPrivateMessage = function(editBtn) {
    const bubble = editBtn.closest('.bubble');
    const messageTextSpan = bubble.querySelector('.message-text');
    const originalText = messageTextSpan.textContent;
    
    // Create edit input
    const editInput = document.createElement('input');
    editInput.type = 'text';
    editInput.value = originalText;
    editInput.className = 'edit-input';
    editInput.style.cssText = 'width: 100%; border: 1px solid #ddd; border-radius: 8px; padding: 8px; margin: 4px 0; font-size: 14px;';
    
    // Create save/cancel buttons
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
    
    // Replace message content with edit interface
    const originalContent = messageTextSpan.innerHTML;
    messageTextSpan.innerHTML = '';
    messageTextSpan.appendChild(editInput);
    messageTextSpan.appendChild(buttonContainer);
    
    // Hide edit button during editing
    editBtn.style.display = 'none';
    
    // Focus on input
    editInput.focus();
    editInput.select();
    
    // Save functionality
    function saveEdit() {
      const newText = editInput.value.trim();
      if (newText && newText !== originalText) {
        messageTextSpan.innerHTML = escapeHtml(newText);
        // Here you would typically send the edit to the server
        // connection.invoke("EditPrivateMessage", messageId, newText);
      } else {
        messageTextSpan.innerHTML = originalContent;
      }
      editBtn.style.display = 'inline-block';
    }
    
    // Cancel functionality
    function cancelEdit() {
      messageTextSpan.innerHTML = originalContent;
      editBtn.style.display = 'inline-block';
    }
    
    // Event listeners
    saveBtn.addEventListener('click', saveEdit);
    cancelBtn.addEventListener('click', cancelEdit);
    
    // Enter to save, Escape to cancel
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

  sendButton.addEventListener('click', sendMessage);
  messageInput.addEventListener('keydown', e=>{ if(e.key==='Enter'){ if(e.shiftKey) return; e.preventDefault(); sendMessage(); }});
  messageInput.addEventListener('input', ()=>{ charCount.textContent=messageInput.value.length; autoGrow(); });
  if(toggleDetails) toggleDetails.addEventListener('click', ()=> document.documentElement.classList.toggle('hide-details'));
  if(btnNewChat) btnNewChat.addEventListener('click', openUserPickerModal);
  if(startNewChatBtn) startNewChatBtn.addEventListener('click', openUserPickerModal);
  if(closeChatBtn) closeChatBtn.addEventListener('click', closeCurrentChat);

  function autoGrow(){ messageInput.style.height='auto'; messageInput.style.height=Math.min(messageInput.scrollHeight,140)+'px'; }

  function sendMessage(){ const text=messageInput.value.trim(); if(!text || !selectedUser) return; if(connection?.state!==signalR.HubConnectionState.Connected){ headerSub.textContent='Bağlı değil'; return;} connection.invoke('SendPrivateMessage', currentUser, selectedUser, text).then(()=>{ addBubble('mine', currentUser, text); upsertConversation(selectedUser); messageInput.value=''; charCount.textContent='0'; autoGrow(); }).catch(err=> console.error('Send error',err)); }

  // Only load conversations (not all users). Provide a modal user picker.
  fetch('/Chat/GetConversations')
    .then(r=>r.json())
    .then(d=>{ if(d.success){ d.conversations.forEach(c=> upsertConversation(c.username,{displayName:c.displayName, profileImageUrl:c.profileImageUrl, isOnline:c.isOnline})); refreshPresence(); }})
    .catch(()=>{});

  function openUserPickerModal(){
    if(!modalEl){ buildUserPickerModal(); }
    modalList.innerHTML=''; modalSpinner.classList.remove('d-none'); modalSearchInput.value=''; modalInstance.show();
    fetch('/Chat/GetUsers').then(r=>r.json()).then(d=>{ modalSpinner.classList.add('d-none'); if(d.success){ userDirectory=d.users; renderModalUsers(userDirectory); } else { modalList.innerHTML='<div class="text-dim small p-3">Kullanıcılar yüklenemedi</div>'; } }).catch(()=>{ modalSpinner.classList.add('d-none'); modalList.innerHTML='<div class="text-dim small p-3">Hata</div>'; });
  }

  function buildUserPickerModal(){
    modalEl=document.createElement('div'); modalEl.className='modal fade'; modalEl.innerHTML=`<div class="modal-dialog modal-dialog-scrollable"><div class="modal-content" style="background:var(--surface);color:var(--text);border:1px solid var(--border);">
      <div class="modal-header"><h6 class="modal-title">Yeni Sohbet</h6><button type="button" class="btn-close" data-bs-dismiss="modal"></button></div>
      <div class="modal-body p-2">
        <div class="px-2 pb-2"><input type="text" class="form-control form-control-sm" id="userPickerSearch" placeholder="Ara..."/></div>
        <div id="userPickerSpinner" class="text-center py-4"><div class="spinner-border spinner-border-sm"></div></div>
        <div id="userPickerList" class="list-group" style="background:transparent;"></div>
      </div>
    </div></div>`;
    document.body.appendChild(modalEl); modalInstance=new bootstrap.Modal(modalEl); modalList=modalEl.querySelector('#userPickerList'); modalSearchInput=modalEl.querySelector('#userPickerSearch'); modalSpinner=modalEl.querySelector('#userPickerSpinner');
    modalSearchInput.addEventListener('input',()=>{ const term=modalSearchInput.value.toLowerCase(); const items=[...modalList.children]; items.forEach(it=>{ it.style.display= it.dataset.search.includes(term)?'flex':'none'; }); });
  }

  function renderModalUsers(users){
    modalList.innerHTML=''; if(!users.length){ modalList.innerHTML='<div class="text-dim small p-3">Kullanıcı yok</div>'; return; }
    users.forEach(u=>{
      const item=document.createElement('button'); item.type='button'; item.className='list-group-item list-group-item-action d-flex align-items-center gap-2'; item.style.background='var(--surface-2)'; item.style.border='1px solid var(--border)';
      const presenceCls = u.isOnline? 'presence-online':'presence-offline';
      item.dataset.search=(u.displayName||u.username).toLowerCase();
      const avatar = u.profileImageUrl? `<img src="${u.profileImageUrl}" class="rounded-circle" width="40" height="40" style="object-fit:cover;">` : `<div class="rounded-circle d-flex align-items-center justify-content-center" style="width:40px;height:40px;background:#1e2732;">${(u.displayName||u.username).charAt(0).toUpperCase()}</div>`;
      item.innerHTML=`${avatar}<div class="flex-grow-1 text-start"><div class="fw-semibold">${u.displayName||u.username}</div><small class="text-dim">${u.isOnline? 'Çevrimiçi':'Çevrimdışı'}</small></div><span class="presence-dot ${presenceCls}" style="position:static;"></span>`;
      item.onclick=()=>{ upsertConversation(u.username,{displayName:u.displayName,profileImageUrl:u.profileImageUrl,isOnline:u.isOnline}); modalInstance.hide(); selectConversation(u.username); };
      modalList.appendChild(item);
    });
  }

  // Show/hide UI states
  function showWelcomeState(){ if(welcomeState) welcomeState.classList.remove('d-none'); if(chatInterface) chatInterface.classList.add('d-none'); }
  function showChatInterface(){ if(welcomeState) welcomeState.classList.add('d-none'); if(chatInterface) chatInterface.classList.remove('d-none'); }
  
  function closeCurrentChat(){
    selectedUser = null;
    messageInput.disabled = true;
    sendButton.disabled = true;
    showWelcomeState();
    renderConversationList(); // Remove active state
  }

  // Lightbox for avatar
  function showImageLightbox(url, title){
    let lb=document.getElementById('imgLightbox'); if(!lb){ lb=document.createElement('div'); lb.id='imgLightbox'; lb.style.cssText='position:fixed;inset:0;background:rgba(0,0,0,.75);display:flex;align-items:center;justify-content:center;z-index:1050;padding:40px;'; lb.innerHTML='<div style="position:relative;max-width:60vw;max-height:80vh;">\n<button type="button" aria-label="Kapat" style="position:absolute;top:-32px;right:-32px;background:#111;padding:6px 10px;border:1px solid var(--border);color:var(--text);border-radius:8px;">×</button>\n<img src="" alt="" style="max-width:100%;max-height:80vh;object-fit:contain;border:1px solid var(--border);border-radius:16px;box-shadow:0 10px 40px -5px #000;"/>\n<div style="text-align:center;margin-top:8px;color:var(--text-dim);font-size:14px;" id="lbCap"></div></div>'; document.body.appendChild(lb); lb.addEventListener('click',e=>{ if(e.target===lb || e.target.tagName==='BUTTON') lb.remove(); }); }
    lb.querySelector('img').src=url; lb.querySelector('#lbCap').textContent=title||''; document.body.appendChild(lb);
  }

  // Tooltips
  document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el=> new bootstrap.Tooltip(el));
  
  // Close chat button event
  if(closeChatBtn) {
    closeChatBtn.addEventListener('click', closeCurrentChat);
  }
})();
