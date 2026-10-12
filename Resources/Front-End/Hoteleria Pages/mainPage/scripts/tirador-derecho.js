import {EVENTO} from "./AcceptedEvents.js";
import {Events} from "./EventsClass.js";

export const chat_container = document.getElementById('chat');
export const thebody = document.getElementsByTagName("body");
export const toggle_chat = document.querySelector(".toggle-chat")
const tirador = document.getElementById('tirador');


const eventAsiggner1 =  new Events(EVENTO.redimensionar, tirador);

const eventAsiggner2= new Events(EVENTO.toggle_chat, toggle_chat)

